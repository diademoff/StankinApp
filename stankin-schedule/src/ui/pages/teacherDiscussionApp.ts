import { ApiClient } from '../../infra/api/ApiClient';
import { AuthWithYandexUseCase } from '../../core/use-cases/AuthWithYandexUseCase';
import { HttpAuthRepository } from '../../infra/repositories/HttpAuthRepository';

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
                const [ratingsResponse, commentsResponse] = await Promise.all([
                    api.getTeacherRatingsByName(this.teacherName),
                    api.getTeacherCommentsByName(this.teacherName)
                ]);

                const ratings = ratingsResponse.data;
                const commentsPage = commentsResponse.data;

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

        async login() {
            console.log('teacherDiscussionApp: Starting Yandex auth'); // Logger: info
            try {
                const authRepository = new HttpAuthRepository(api); // api - экземпляр ApiClient
                const authUseCase = new AuthWithYandexUseCase(authRepository);

                const initResult = await window.YaAuthSuggest.init({
                    client_id: 'b8cf30c02ab34703a93e776050c904f7',
                    response_type: 'token',
                    redirect_uri: `${window.location.origin}/auth-callback.html`
                }, window.location.origin, {
                    view: 'default' // Или 'button', если нужно рендерить кнопку
                });

                if (!initResult) {
                    throw new Error('Yandex SDK init failed'); // Validation: fail
                }
                console.log('teacherDiscussionApp: SDK initialized successfully'); // Validation: success

                const authData = await initResult.handler();
                if (!authData || !authData.access_token) {
                    throw new Error('No access_token received from Yandex'); // Validation: fail
                }
                console.log('teacherDiscussionApp: Yandex token received'); // Validation: success

                const jwt = await authUseCase.execute(authData.access_token);
                if (!jwt) {
                    throw new Error('Failed to get JWT from backend'); // Validation: fail
                }
                localStorage.setItem('jwt_token', jwt);
                this.user.loggedIn = true;
                console.log('teacherDiscussionApp: JWT saved, user logged in'); // Validation: success

                await this.fetchData(); // Обновить данные с auth headers
            } catch (error) {
                console.error('teacherDiscussionApp: Auth failed', error); // Logger: error
                this.error = 'Ошибка авторизации через Yandex. Попробуйте позже.';
            }
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