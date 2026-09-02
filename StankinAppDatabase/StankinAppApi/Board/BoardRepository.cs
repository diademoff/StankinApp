using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StankinAppApi.Board;

public class ThreadSummary
{
    public Post Op { get; set; } = new();
    public int ReplyCount { get; set; }
    public List<Post> LastReplies { get; set; } = new();
    public bool IsPinned { get; set; }
}

public class BoardRepository
{
    private const string PostColumns =
        "id, thread_id, parent_id, text, created_at, updated_at, is_deleted, report_count, ip_hash";

    private readonly string _dbPath;
    private readonly HashSet<long> _pinned;
    private long _rev;

    // ponytail: общий rev вместо точечной инвалидации кэша — записи редкие,
    // ключи с rev сами вымываются TTL кэша чтений
    public long Revision => Volatile.Read(ref _rev);

    private void BumpRev() => Interlocked.Increment(ref _rev);

    public BoardRepository(string dbPath, IReadOnlyCollection<long> pinnedThreadIds = null)
    {
        _dbPath = dbPath;
        _pinned = pinnedThreadIds == null ? new HashSet<long>() : new HashSet<long>(pinnedThreadIds);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    public void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS posts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thread_id INTEGER,
                parent_id INTEGER,
                text TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                report_count INTEGER NOT NULL DEFAULT 0,
                ip_hash TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_posts_thread ON posts(thread_id);
            CREATE INDEX IF NOT EXISTS idx_posts_updated ON posts(updated_at);
            CREATE TABLE IF NOT EXISTS banned_ips (
                ip_hash TEXT PRIMARY KEY,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS captcha_usage (
                month TEXT PRIMARY KEY,
                count INTEGER NOT NULL DEFAULT 0
            );";
        cmd.ExecuteNonQuery();
    }

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static Post ReadPost(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        ThreadId = r.IsDBNull(1) ? null : r.GetInt64(1),
        ParentId = r.IsDBNull(2) ? null : r.GetInt64(2),
        Text = r.GetString(3),
        CreatedAt = ParseDate(r.GetString(4)),
        UpdatedAt = ParseDate(r.GetString(5)),
        IsDeleted = r.GetInt64(6) != 0,
        ReportCount = r.GetInt32(7),
        IpHash = r.GetString(8)
    };

    private Post GetPost(SqliteConnection conn, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {PostColumns} FROM posts WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadPost(r) : null;
    }

    private static List<Post> ReadAll(SqliteConnection conn, string sql, params SqliteParameter[] parameters)
    {
        var posts = new List<Post>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        using var r = cmd.ExecuteReader();
        while (r.Read()) posts.Add(ReadPost(r));
        return posts;
    }

    // последние 3 ответа для всех тредов страницы одним запросом вместо N+1
    private static Dictionary<long, List<Post>> LoadLastReplies(SqliteConnection conn, IReadOnlyList<long> threadIds)
    {
        var map = new Dictionary<long, List<Post>>();
        if (threadIds.Count == 0) return map;
        using var cmd = conn.CreateCommand();
        // id берутся из БД (не от клиента) — безопасны для IN-списка
        cmd.CommandText = $@"SELECT {PostColumns} FROM posts
                             WHERE thread_id IN ({string.Join(",", threadIds)}) AND is_deleted = 0
                             ORDER BY id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var p = ReadPost(r);
            var tid = p.ThreadId!.Value;
            if (!map.TryGetValue(tid, out var list))
                map[tid] = list = new List<Post>();
            if (list.Count < 3) list.Add(p); // DESC → первые 3 и есть последние ответы
        }
        return map;
    }

    public List<ThreadSummary> GetThreads(int page, int pageSize)
    {
        var ops = new List<(Post Op, int ReplyCount)>();
        using var conn = Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"SELECT {PostColumns},
                        (SELECT COUNT(*) FROM posts r WHERE r.thread_id = p.id) AS reply_count
                    FROM posts p
                    WHERE p.thread_id IS NULL AND p.is_deleted = 0
                    ORDER BY CASE WHEN p.id IN (SELECT value FROM json_each(@pinned)) THEN 0 ELSE 1 END, p.updated_at DESC
                    LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@pinned", JsonSerializer.Serialize(_pinned));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var op = ReadPost(r);
                ops.Add((op, r.GetInt32(9)));
            }
        }
        var lastReplies = LoadLastReplies(conn, ops.Select(o => o.Op.Id).ToList());
        return ops.Select(o =>
        {
            lastReplies.TryGetValue(o.Op.Id, out var replies);
            var last = replies ?? new List<Post>();
            last.Reverse(); // DESC-выборка → хронологический порядок
            return new ThreadSummary
            {
                Op = o.Op,
                ReplyCount = o.ReplyCount,
                IsPinned = _pinned.Contains(o.Op.Id),
                LastReplies = last
            };
        }).ToList();
    }

    public List<Post> GetThread(long threadId)
    {
        using var conn = Open();
        return ReadAll(conn,
            $"SELECT {PostColumns} FROM posts WHERE id = @tid OR thread_id = @tid ORDER BY id",
            new SqliteParameter("@tid", threadId));
    }

    public Post CreateThread(string text, string ipHash)
    {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO posts (thread_id, parent_id, text, created_at, updated_at, ip_hash)
                            VALUES (NULL, NULL, @text, @now, @now, @ip);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@text", text);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@ip", ipHash);
        var id = (long)cmd.ExecuteScalar();
        BumpRev();
        return GetPost(conn, id);
    }

    public (Post Post, bool Bumped) AddReply(long threadId, long? parentId, string text, string ipHash, bool sage, int bumpLimit)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id FROM posts WHERE id = @tid AND thread_id IS NULL AND is_deleted = 0";
            cmd.Parameters.AddWithValue("@tid", threadId);
            if (cmd.ExecuteScalar() == null)
                throw new KeyNotFoundException("Thread not found");
        }

        if (parentId != null)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id FROM posts WHERE id = @pid AND (id = @tid OR thread_id = @tid)";
            cmd.Parameters.AddWithValue("@pid", parentId.Value);
            cmd.Parameters.AddWithValue("@tid", threadId);
            if (cmd.ExecuteScalar() == null)
                throw new ArgumentException("parentId не принадлежит треду");
        }

        long replyCount;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COUNT(*) FROM posts WHERE thread_id = @tid";
            cmd.Parameters.AddWithValue("@tid", threadId);
            replyCount = (long)cmd.ExecuteScalar();
        }

        var now = DateTime.UtcNow;
        var nowStr = now.ToString("O", CultureInfo.InvariantCulture);

        long id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO posts (thread_id, parent_id, text, created_at, updated_at, ip_hash)
                                VALUES (@tid, @pid, @text, @now, @now, @ip);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@tid", threadId);
            cmd.Parameters.AddWithValue("@pid", parentId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@text", text);
            cmd.Parameters.AddWithValue("@now", nowStr);
            cmd.Parameters.AddWithValue("@ip", ipHash);
            id = (long)cmd.ExecuteScalar();
        }

        var bumped = !sage && replyCount < bumpLimit;
        if (bumped)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE posts SET updated_at = @now WHERE id = @tid";
            cmd.Parameters.AddWithValue("@now", nowStr);
            cmd.Parameters.AddWithValue("@tid", threadId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        BumpRev();
        return (GetPost(conn, id), bumped);
    }

    public bool Report(long postId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE posts SET report_count = report_count + 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", postId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public int CountNewThreads(DateTime? since)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = since == null
            ? "SELECT COUNT(*) FROM posts WHERE thread_id IS NULL AND is_deleted = 0"
            : "SELECT COUNT(*) FROM posts WHERE thread_id IS NULL AND is_deleted = 0 AND created_at > @since";
        if (since != null)
            cmd.Parameters.AddWithValue("@since", since.Value.ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<Post> GetReports()
    {
        using var conn = Open();
        return ReadAll(conn,
            $"SELECT {PostColumns} FROM posts WHERE report_count > 0 AND is_deleted = 0 ORDER BY report_count DESC, id");
    }

    public bool SoftDelete(long postId)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        long? threadId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT thread_id FROM posts WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", postId);
            var scalar = cmd.ExecuteScalar();
            if (scalar == null)
                return false;
            threadId = scalar == DBNull.Value ? null : (long)scalar;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = threadId == null
                ? "UPDATE posts SET is_deleted = 1 WHERE id = @id OR thread_id = @id"
                : "UPDATE posts SET is_deleted = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", postId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        BumpRev();
        return true;
    }

    public bool DismissReports(long postId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE posts SET report_count = 0 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", postId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public void Ban(string ipHash)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO banned_ips (ip_hash, created_at) VALUES (@h, @now) ON CONFLICT(ip_hash) DO NOTHING";
        cmd.Parameters.AddWithValue("@h", ipHash);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public bool IsBanned(string ipHash)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM banned_ips WHERE ip_hash = @h";
        cmd.Parameters.AddWithValue("@h", ipHash);
        return cmd.ExecuteScalar() != null;
    }

    public int IncrementCaptcha(string month)
    {
        using var conn = Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO captcha_usage (month, count) VALUES (@m, 1)
                                ON CONFLICT(month) DO UPDATE SET count = count + 1";
            cmd.Parameters.AddWithValue("@m", month);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count FROM captcha_usage WHERE month = @m";
            cmd.Parameters.AddWithValue("@m", month);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
