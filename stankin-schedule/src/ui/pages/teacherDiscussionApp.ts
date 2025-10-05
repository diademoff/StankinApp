// stankin-schedule/src/ui/pages/teacherDiscussionApp.ts

import { ApiClient } from '../../infra/api/ApiClient';

export function teacherDiscussionApp(api: ApiClient) {
    return {
        teacherName: null as string | null,
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
            const teacherNameParam = params.get('teacherName');
            if (!teacherNameParam) {
                this.error = "Имя преподавателя не найдено в URL.";
                this.loading = false;
                return;
            }
            // Декодируем имя из URL
            this.teacherName = decodeURIComponent(teacherNameParam);
            this.fetchData();
        },

        async fetchData() {
            if (!this.teacherName) return;
            this.loading = true;
            this.error = null;
            try {
                // Сначала проверяем существование преподавателя в основном расписании (schedule.db)
                // Для этого нам нужен новый метод в ApiClient
                const isValid = await api.validateTeacher(this.teacherName);
                if (!isValid) {
                    throw new Error("Преподаватель не найден в расписании.");
                }

                // Теперь загружаем данные из PostgreSQL
                const [ratings, commentsPage] = await Promise.all([
                    api.getTeacherRatingsByName(this.teacherName),
                    api.getTeacherCommentsByName(this.teacherName)
                ]);

                this.teacher = {
                    name: this.teacherName,
                    averageRating: ratings.averageScore,
                    totalRatings: ratings.ratingsCount
                };
                this.comments = commentsPage.comments || [];
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
            if (!this.user.loggedIn || !this.teacherName) return;
            try {
                await api.postRatingByName(this.teacherName, score);
                this.user.ownRating = score;
                this.fetchData(); // Обновляем данные
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
            if (!this.user.loggedIn || !this.newCommentText.trim() || !this.teacherName) return;
            try {
                await api.postCommentByName(this.teacherName, this.newCommentText.trim(), false);
                this.newCommentText = '';
                this.fetchData();
            } catch (e) {
                alert("Не удалось отправить комментарий.");
                console.error(e);
            }
        }
    }
}