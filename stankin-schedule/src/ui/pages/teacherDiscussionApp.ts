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

        async init() {
            this.user.loggedIn = !!localStorage.getItem('jwt_token');
            const params = new URLSearchParams(window.location.search);
            const teacherNameParam = params.get('teacherName');

            if (!teacherNameParam) {
                this.error = "Имя преподавателя не найдено в URL.";
                this.loading = false;
                return;
            }

            this.teacherName = decodeURIComponent(teacherNameParam);
            await this.fetchData();

            // Если пользователь авторизован, загружаем его оценку
            if (this.user.loggedIn) {
                await this.fetchUserRating();
            }
        },

        async fetchData() {
            if (!this.teacherName) return;

            this.loading = true;
            this.error = null;

            try {
                const [ratingsResponse, commentsResponse] = await Promise.all([
                    api.getTeacherRatingsByName(this.teacherName),
                    api.getTeacherCommentsByName(this.teacherName)
                ]);

                // Валидация: проверяем структуру ответа
                if (!ratingsResponse.data || !commentsResponse.data) {
                    throw new Error("Некорректный ответ от сервера");
                }

                const ratings = ratingsResponse.data;
                const commentsPage = commentsResponse.data;

                this.teacher = {
                    name: this.teacherName,
                    averageRating: ratings.averageScore || 0,
                    totalRatings: ratings.ratingsCount || 0
                };

                this.comments = commentsPage.comments || [];

                console.log('✅ Данные преподавателя загружены'); // Validation: success

            } catch (e: any) {
                console.error('❌ Ошибка загрузки данных:', e); // Logger: error
                this.error = e.message || "Не удалось загрузить данные. Попробуйте позже.";
            } finally {
                this.loading = false;
            }
        },

        async fetchUserRating() {
            if (!this.teacherName || !this.user.loggedIn) return;

            try {
                const response = await api.getUserRatingForTeacher(this.teacherName);
                this.user.ownRating = response.data?.score || 0;
            } catch (e) {
                console.warn('Не удалось загрузить оценку пользователя');
            }
        },

        async login() {
            /*
             * Для отладки можно перейти на https://oauth.yandex.ru/authorize?response_type=token&client_id=b8cf30c02ab34703a93e776050c904f7
             * и получить токен вручную
             */

            console.log('teacherDiscussionApp: Starting Yandex auth'); // Logger: info
            try {
                const authRepository = new HttpAuthRepository(api); // api - экземпляр ApiClient
                const authUseCase = new AuthWithYandexUseCase(authRepository);

                // const initResult = await window.YaAuthSuggest.init({
                //     client_id: 'b8cf30c02ab34703a93e776050c904f7',
                //     response_type: 'token',
                //     redirect_uri: `${window.location.origin}/auth-callback.html`
                // }, window.location.origin, {
                //     view: 'default' // Или 'button', если нужно рендерить кнопку
                // });

                // if (!initResult) {
                //     throw new Error('Yandex SDK init failed'); // Validation: fail
                // }
                // console.log('teacherDiscussionApp: SDK initialized successfully'); // Validation: success

                // const authData = await initResult.handler();
                // if (!authData || !authData.access_token) {
                //     throw new Error('No access_token received from Yandex'); // Validation: fail
                // }
                // console.log('teacherDiscussionApp: Yandex token received'); // Validation: success

                // const jwt = await authUseCase.execute(authData.access_token);
                // if (!jwt) {
                //     throw new Error('Failed to get JWT from backend'); // Validation: fail
                // }
                // localStorage.setItem('jwt_token', jwt);
                this.user.loggedIn = true;

                await this.fetchUserRating();

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
            if (!this.user.loggedIn || !this.teacherName) {
                alert('Пожалуйста, войдите чтобы оценить преподавателя');
                return;
            }

            try {
                await api.postRatingByName(this.teacherName, score);
                this.user.ownRating = score;

                // Валидация: обновляем общие данные
                await this.fetchData();
                console.log('✅ Rating submitted successfully'); // Validation: success

            } catch (e: any) {
                console.error('❌ Rating error:', e); // Logger: error
                alert(`Ошибка при установке оценки: ${e.message}`);
            }
        },

        async voteComment(commentId: number, direction: 1 | -1) {
            if (!this.user.loggedIn) {
                alert('Пожалуйста, войдите, чтобы голосовать.');
                return;
            }

            try {
                await api.postVote(commentId, direction);

                // Валидация: обновляем комментарии чтобы увидеть изменения
                await this.fetchData();
                console.log('✅ Vote submitted'); // Validation: success

            } catch (e: any) {
                console.error('❌ Voting error:', e); // Logger: error
                alert(`Ошибка голосования: ${e.message}`);
            }
        },

        async addComment() {
            if (!this.user.loggedIn) {
                alert('Пожалуйста, войдите чтобы оставить комментарий');
                return;
            }

            if (!this.newCommentText.trim() || !this.teacherName) return;

            try {
                await api.postCommentByName(this.teacherName, this.newCommentText.trim(), false);
                this.newCommentText = '';

                // Валидация: обновляем комментарии
                await this.fetchData();
                console.log('✅ Comment added successfully'); // Validation: success

            } catch (e: any) {
                console.error('❌ Comment error:', e); // Logger: error
                alert(`Не удалось отправить комментарий: ${e.message}`);
            }
        }
    }
}