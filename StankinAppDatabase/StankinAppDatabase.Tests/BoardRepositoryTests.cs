using StankinAppApi.Board;

namespace StankinAppDatabase.Tests;

[TestFixture]
public class BoardRepositoryTests
{
    private static BoardRepository CreateRepo(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"board_{Guid.NewGuid():N}.db");
        var repo = new BoardRepository(path);
        repo.EnsureSchema();
        return repo;
    }

    private static void Cleanup(string path)
    {
        File.Delete(path);
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
    }

    [Test]
    public void AddReply_BumpsOnlyWhileUnderLimit()
    {
        var repo = CreateRepo(out var path);
        try
        {
            var op = repo.CreateThread("op", "hashA");
            var bumps = new List<bool>();
            for (int i = 0; i < 5; i++)
            {
                var (_, bumped) = repo.AddReply(op.Id, null, $"reply {i}", "hashB", sage: false, bumpLimit: 3);
                bumps.Add(bumped);
            }
            Assert.That(bumps, Is.EqualTo(new[] { true, true, true, false, false }));
        }
        finally { Cleanup(path); }
    }

    [Test]
    public void AddReply_SageDoesNotBump()
    {
        var repo = CreateRepo(out var path);
        try
        {
            var op = repo.CreateThread("op", "hashA");
            var (_, bumped) = repo.AddReply(op.Id, null, "sage", "hashB", sage: true, bumpLimit: 50);
            Assert.That(bumped, Is.False);
        }
        finally { Cleanup(path); }
    }

    [Test]
    public void SoftDelete_Op_RemovesWholeThread()
    {
        var repo = CreateRepo(out var path);
        try
        {
            var op = repo.CreateThread("op", "hashA");
            var reply = repo.AddReply(op.Id, null, "r", "hashB", sage: false, bumpLimit: 50).Post;
            Assert.That(repo.SoftDelete(op.Id), Is.True);
            Assert.That(repo.GetThread(op.Id), Has.All.Matches<Post>(p => p.IsDeleted));
            Assert.That(repo.GetThreads(1, 20), Is.Empty);
        }
        finally { Cleanup(path); }
    }

    [Test]
    public void SoftDelete_Reply_KeepsSiblingsVisible()
    {
        var repo = CreateRepo(out var path);
        try
        {
            var op = repo.CreateThread("op", "hashA");
            repo.AddReply(op.Id, null, "r1", "hashB", sage: false, bumpLimit: 50);
            var r2 = repo.AddReply(op.Id, null, "r2", "hashB", sage: false, bumpLimit: 50).Post;
            Assert.That(repo.SoftDelete(r2.Id), Is.True);
            var posts = repo.GetThread(op.Id);
            Assert.That(posts.Count, Is.EqualTo(3));
            Assert.That(posts.First(p => p.Id == r2.Id).IsDeleted, Is.True);
        }
        finally { Cleanup(path); }
    }
}
