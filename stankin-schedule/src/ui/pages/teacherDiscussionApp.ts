// stankin-schedule/src/ui/pages/teacherDiscussionApp.ts

interface TeacherData {
    id: number;
    name: string;
    averageRating: number;
    totalRatings: number;
    comments: CommentData[];
}

interface CommentData {
    id: number;
    author: string;
    content: string;
    votes: number;
    userVote: number;
    createdAt: string;
    replies: ReplyData[];
}

interface ReplyData {
    id: number;
    author: string;
    content: string;
    createdAt: string;
}

// TODO: ЗАГЛУШКА: Данные, которые в будущем придут с API
const MOCK_TEACHERS_DATA: Record<string, TeacherData> = {
    '1': {
        id: 1,
        name: 'Иванов И.И.',
        averageRating: 4.8,
        totalRatings: 125,
        comments: [
            {
                id: 101, author: 'Анонимный Физик', content: 'Лучший преподаватель по Web-приложениям! Всегда помогает и объясняет понятно.', votes: 42, userVote: 0, createdAt: '20.09.2025', replies: [
                    { id: 301, author: 'Анонимный Химик', content: 'Полностью согласен!', createdAt: '21.09.2025' }
                ]
            },
            { id: 102, author: 'Анонимный Математик', content: 'Материал сложный, но лекции интересные. Требовательный, но справедливый.', votes: 15, userVote: 0, createdAt: '15.09.2025', replies: [] },
            { id: 103, author: 'Анонимный Инженер', content: 'На лабораторных работах иногда бывает сложно, но это того стоит.', votes: -2, userVote: 0, createdAt: '18.09.2025', replies: [] },
        ]
    },
    '2': {
        id: 0,
        name: 'Аристархов П.В.',
        averageRating: 4.5,
        totalRatings: 98,
        comments: [
            { id: 201, author: 'Анонимный Студент', content: 'Хорошо объясняет физику, наглядно и с примерами.', votes: 25, userVote: 0, createdAt: '22.09.2025', replies: [] },
        ]
    }
};


export function teacherDiscussionApp() {
    return {
        teacher: null as any | null,
        comments: [] as any[],
        user: {
            loggedIn: false,
            ownRating: 0,
        },
        newCommentText: '',
        newReplyText: {} as Record<number, string>,
        showReplyForm: null as number | null,

        get sortedComments() {
            return this.comments.slice().sort((a, b) => b.votes - a.votes);
        },

        init() {
            console.log('teacherDiscussionApp.init');
            const params = new URLSearchParams(window.location.search);
            const teacherId = params.get('teacherId');

            // Загружаем данные из заглушки
            if (teacherId && MOCK_TEACHERS_DATA[teacherId]) {
                const data = MOCK_TEACHERS_DATA[teacherId];
                this.teacher = {
                    id: data.id,
                    name: data.name,
                    averageRating: data.averageRating,
                    totalRatings: data.totalRatings
                };
                this.comments = data.comments;
            }
        },

        login() {
            // ЗАГЛУШКА: в реальности здесь будет логика Telegram
            this.user.loggedIn = true;
            console.log('User logged in (mock)');
        },

        rateTeacher(score: number) {
            if (!this.user.loggedIn) return;
            this.user.ownRating = score;
            console.log(`Rated teacher ${this.teacher.id} with ${score}`);
        },

        voteComment(commentId: number, direction: 1 | -1) {
            if (!this.user.loggedIn) {
                alert('Пожалуйста, войдите, чтобы голосовать.');
                return;
            }
            const comment = this.comments.find(c => c.id === commentId);
            if (!comment) return;

            // Если пользователь голосует повторно за то же самое - отменяем голос
            if (comment.userVote === direction) {
                comment.votes -= direction;
                comment.userVote = 0;
            } else {
                // Убираем старый голос, если он был
                if (comment.userVote !== 0) {
                    comment.votes -= comment.userVote;
                }
                // Добавляем новый голос
                comment.votes += direction;
                comment.userVote = direction;
            }
        },

        addComment() {
            if (!this.user.loggedIn || !this.newCommentText.trim()) return;

            const newComment = {
                id: Date.now(),
                author: 'Вы (Анонимно)',
                content: this.newCommentText,
                votes: 1,
                userVote: 1,
                createdAt: new Date().toLocaleDateString('ru-RU'),
                replies: []
            };

            this.comments.unshift(newComment);
            this.newCommentText = '';
        },

        addReply(parentCommentId: number) {
            const content = (this.newReplyText[parentCommentId] || '').trim();
            if (!this.user.loggedIn || !content) return;

            const parentComment = this.comments.find(c => c.id === parentCommentId);
            if (!parentComment) return;

            const newReply = {
                id: Date.now(),
                author: 'Вы (Анонимно)',
                content: content,
                createdAt: new Date().toLocaleDateString('ru-RU'),
            };

            parentComment.replies.push(newReply);
            this.newReplyText[parentCommentId] = '';
            this.showReplyForm = null;
        }
    }
}