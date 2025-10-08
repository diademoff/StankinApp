class AuthService {
    private jwt: string | null = null;
    private static instance: AuthService;

    private constructor() {
        // const token = localStorage.getItem('jwt_token');
        // Private constructor for singleton
    }

    public static getInstance(): AuthService {
        if (!AuthService.instance) {
            AuthService.instance = new AuthService();
        }
        return AuthService.instance;
    }

    setToken(token: string): void {
        this.jwt = token;
        console.log('AuthService: Token set in memory');
    }

    getToken(): string | null {
        return this.jwt;
    }

    isLoggedIn(): boolean {
        return !!this.jwt;
    }

    logout(): void {
        this.jwt = null;
        console.log('AuthService: User logged out, token cleared from memory');
        // Опционально: перезагрузить страницу для сброса состояния
        window.location.reload();
    }
}

export const authService = AuthService.getInstance();