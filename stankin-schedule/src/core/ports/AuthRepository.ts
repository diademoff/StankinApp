export interface AuthRepository {
  exchangeYandexToken(token: string): Promise<string>; // Возвращает JWT
}