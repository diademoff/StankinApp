import { ApiClient } from '../../infra/api/ApiClient';
import { AuthWithYandexUseCase } from '../../core/use-cases/AuthWithYandexUseCase';
import { authService } from '../../shared/AuthService';

// To avoid TypeScript errors for Yandex SDK
declare global {
    interface Window {
        YaAuthSuggest: any;
    }
}

export function teacherDiscussionApp(api: ApiClient, authUseCase: AuthWithYandexUseCase) {
    return {
        teacherName: null as string | null,
        teacher: null as any | null,
        loading: true,
        error: null as string | null,
        user: {
            loggedIn: false,
            ownRating: 0,
        },

        async init() {
            this.user.loggedIn = authService.isLoggedIn();
            const params = new URLSearchParams(window.location.search);
            const teacherNameParam = params.get('teacherName');

            if (!teacherNameParam) {
                this.error = "Имя преподавателя не найдено в URL.";
                this.loading = false;
                return;
            }

            this.teacherName = decodeURIComponent(teacherNameParam);
            await this.fetchData();

            if (this.user.loggedIn) {
                await this.fetchUserRating();
            }
        },

        async fetchData() {
            if (!this.teacherName) return;
            this.loading = true;
            this.error = null;

            try {
                const ratingsResponse = await api.getTeacherRating(this.teacherName);
                if (!ratingsResponse.data) {
                    throw new Error("Некорректный ответ от сервера при загрузке рейтинга");
                }

                const ratings = ratingsResponse.data;

                this.teacher = {
                    name: this.teacherName,
                    averageRating: ratings.averageScore?.toFixed(1) || '–',
                    totalRatings: ratings.ratingsCount || 0
                };

            } catch (e: any) {
                console.error('❌ Ошибка загрузки данных:', e);
                this.error = e.message || "Не удалось загрузить данные. Попробуйте позже.";
            } finally {
                this.loading = false;
            }
        },

        async fetchUserRating() {
            if (!this.teacherName || !this.user.loggedIn) return;
            try {
                const response = await api.getUserRating(this.teacherName);
                this.user.ownRating = response.data?.score || 0;
            } catch (e) {
                console.warn('Не удалось загрузить оценку пользователя. Возможно, она еще не поставлена.');
                this.user.ownRating = 0;
            }
        },

        async login() {
            /*
             * Для отладки можно перейти на https://oauth.yandex.ru/authorize?response_type=token&client_id=b8cf30c02ab34703a93e776050c904f7
             * и получить токен вручную
             */
            try {
                const YANDEX_CLIENT_ID = "b8cf30c02ab34703a93e776050c904f7";
                const initResult = await window.YaAuthSuggest.init({
                    client_id: YANDEX_CLIENT_ID,
                    response_type: 'token',
                    redirect_uri: `${window.location.origin}/auth-callback.html`
                }, window.location.origin);

                if (!initResult) throw new Error('Yandex SDK init failed');

                const authData = await initResult.handler();
                if (!authData || !authData.access_token) throw new Error('No access_token received from Yandex');

                const authToken = await authUseCase.execute(authData.access_token);
                if (!authToken) throw new Error('Failed to get auth token from backend');

                authService.setToken(authToken);
                this.user.loggedIn = true;

                await this.fetchUserRating();
                await this.fetchData();
            } catch (error) {
                console.error('teacherDiscussionApp: Auth failed', error); // Logger: error
                this.error = 'Ошибка авторизации через Yandex. Попробуйте позже.';
            }
        },

        logout() {
            authService.logout();
        },

        async rateTeacher(score: number) {
            if (!this.user.loggedIn || !this.teacherName) {
                alert('Пожалуйста, войдите, чтобы оценить преподавателя.');
                return;
            }

            try {
                await api.postTeacherRating(this.teacherName, score);
                this.user.ownRating = score;
                await this.fetchData(); // Refresh overall rating

            } catch (e: any) {
                console.error('Rating error:', e);
                alert(`Ошибка при установке оценки: ${e.message}`);
            }
        }
    }
}