// stankin-schedule/src/ui/pages/teacherDiscussionApp.ts

import { ApiClient } from '../../infra/api/ApiClient';

export function teacherDiscussionApp(api: ApiClient) {
    return {
        teacherId: null as number | null,
        teacher: null as any | null,
        comments: [] as any[],
        loading: true,
        error: null as string | null,
        user: {
            loggedIn: false,
            ownRating: 0, // Тут можно будет хранить оценку пользователя
        },
        newCommentText: '',

        get sortedComments() {
            return this.comments.slice().sort((a, b) => b.votes - a.votes);
        },

        init() {
            this.user.loggedIn = !!localStorage.getItem('jwt_token');
            const params = new URLSearchParams(window.location.search);
            const teacherIdParam = params.get('teacherId');

            if (!teacherIdParam) {
                this.error = "ID преподавателя не найден в URL.";
                this.loading = false;
                return;
            }
            this.teacherId = parseInt(teacherIdParam, 10);
            this.fetchData();
        },

        async fetchData() {
            if (!this.teacherId) return;
            this.loading = true;
            this.error = null;
            try {
                // Загружаем список всех преподавателей, чтобы найти имя по ID
                const teachersList = await api.getTeachers();
                const teacherName = teachersList[this.teacherId - 1]; // ID в URL 1-based, массив 0-based

                if (!teacherName) throw new Error("Преподаватель не найден");

                // Параллельно загружаем рейтинг и комментарии
                const [ratings, commentsPage] = await Promise.all([
                    api.getTeacherRatings(this.teacherId),
                    api.getTeacherComments(this.teacherId)
                ]);

                this.teacher = {
                    id: this.teacherId,
                    name: teacherName,
                    averageRating: ratings.averageScore.toFixed(1),
                    totalRatings: ratings.ratingsCount
                };
                this.comments = commentsPage.comments;

            } catch (e: any) {
                this.error = e.message || "Не удалось загрузить данные. Попробуйте позже.";
                console.error(e);
            } finally {
                this.loading = false;
            }
        },

        login() {
            // Редирект на бэкенд для начала процесса авторизации
            // Бэкенд вернет пользователя на текущую страницу
            const currentPath = window.location.pathname + window.location.search;
            window.location.href = `/api/auth/yandex/login?from=${encodeURIComponent(currentPath)}`;
        },

        logout() {
            localStorage.removeItem('jwt_token');
            this.user.loggedIn = false;
            // Можно перезагрузить страницу или просто обновить UI
            window.location.reload();
        },

        async rateTeacher(score: number) {
            if (!this.user.loggedIn || !this.teacherId) return;
            try {
                await api.postRating(this.teacherId, score);
                this.user.ownRating = score; // Оптимистичное обновление
                // Обновляем общую статистику
                this.fetchData();
            } catch (e) {
                alert("Ошибка при установке оценки.");
                console.error(e);
            }
        },

        async voteComment(commentId: number, direction: 1 | -1) {
            if (!this.user.loggedIn) {
                alert('Пожалуйста, войдите, чтобы голосовать.');
                return;
            }
            try {
                await api.postVote(commentId, direction);
                // Обновляем данные, чтобы увидеть результат
                this.fetchData();
            } catch (e: any) {
                alert(`Ошибка голосования: ${e.message}`);
                console.error(e);
            }
        },

        async addComment() {
            if (!this.user.loggedIn || !this.newCommentText.trim() || !this.teacherId) return;
            try {
                await api.postComment(this.teacherId, this.newCommentText.trim(), false); // Anonymous false for now
                this.newCommentText = '';
                // Обновляем комментарии
                this.fetchData();
            } catch (e) {
                alert("Не удалось отправить комментарий.");
                console.error(e);
            }
        },
    }
}