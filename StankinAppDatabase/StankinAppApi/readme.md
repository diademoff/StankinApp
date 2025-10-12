PostgreSql db:

```sql
-- Убедимся, что таблица teachers существует (id — integer)
-- Database: stankin

-- DROP DATABASE IF EXISTS stankin;

-- CREATE DATABASE stankin
--     WITH
--     OWNER = postgres
--     ENCODING = 'UTF8'
--     LC_COLLATE = 'ru_RU.UTF-8'
--     LC_CTYPE = 'ru_RU.UTF-8'
--     LOCALE_PROVIDER = 'libc'
--     TABLESPACE = pg_default
--     CONNECTION LIMIT = -1
--     IS_TEMPLATE = False;

CREATE TABLE IF NOT EXISTS Teachers (
        "Id" SERIAL PRIMARY KEY,
        "Name" TEXT NOT NULL UNIQUE
	);

-- Users table (Yandex authorization)
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" SERIAL PRIMARY KEY,
    "YandexId" BIGINT NOT NULL UNIQUE,
    "FirstName" VARCHAR(255) NOT NULL,
    "Username" VARCHAR(255),
    "PhotoUrl" TEXT,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create index for faster lookups
CREATE INDEX IF NOT EXISTS idx_users_Yandex_id ON "Users"("YandexId");

-- Ratings table
CREATE TABLE IF NOT EXISTS "Ratings" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "TeacherId" INT NOT NULL,
    "TeacherName" VARCHAR(500) NOT NULL, -- Store teacher name for reference
    "Score" SMALLINT NOT NULL CHECK ("Score" >= 1 AND "Score" <= 10),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE("UserId", "TeacherId")
);

-- Indexes for ratings
CREATE INDEX IF NOT EXISTS idx_ratings_teacher_id ON "Ratings"("TeacherId");
CREATE INDEX IF NOT EXISTS idx_ratings_user_id ON "Ratings"("UserId");

-- Comments table
CREATE TABLE IF NOT EXISTS "Comments" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "TeacherId" INT NOT NULL,
    "TeacherName" VARCHAR(500) NOT NULL,
    "Anonymous" BOOLEAN NOT NULL DEFAULT FALSE,
    "Content" TEXT NOT NULL CHECK (char_length("Content") BETWEEN 1 AND 5000),
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for comments
CREATE INDEX IF NOT EXISTS idx_comments_teacher_id ON "Comments"("TeacherId");
CREATE INDEX IF NOT EXISTS idx_comments_user_id ON "Comments"("UserId");
CREATE INDEX IF NOT EXISTS idx_comments_created_at ON "Comments"("CreatedAt" DESC);

-- Comment votes table
CREATE TABLE IF NOT EXISTS "CommentVotes" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "CommentId" INT NOT NULL REFERENCES "Comments"("Id") ON DELETE CASCADE,
    "Vote" SMALLINT NOT NULL CHECK ("Vote" IN (-1, 1)),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE("UserId", "CommentId")
);

-- Indexes for votes
CREATE INDEX IF NOT EXISTS idx_comment_votes_comment_id ON "CommentVotes"("CommentId");
CREATE INDEX IF NOT EXISTS idx_comment_votes_user_id ON "CommentVotes"("UserId");

-- Add update timestamp trigger function
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Add triggers for updating UpdatedAt
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON "Users"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_ratings_updated_at BEFORE UPDATE ON "Ratings"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_comments_updated_at BEFORE UPDATE ON "Comments"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_comment_votes_updated_at BEFORE UPDATE ON "CommentVotes"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Добавляем индекс для быстрого поиска оценок пользователя по преподавателю
CREATE INDEX IF NOT EXISTS idx_ratings_user_teacher
ON "Ratings" ("UserId", "TeacherId");

-- Индекс для поиска преподавателей по имени
CREATE INDEX IF NOT EXISTS idx_teachers_name
ON Teachers ("Name");
```