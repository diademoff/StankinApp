import { AuthRepository } from '../ports/AuthRepository';

export class AuthWithYandexUseCase {
  constructor(private repo: AuthRepository) {}

  async execute(token: string): Promise<string> {
    console.log('AuthWithYandexUseCase: Starting token exchange'); // Logger: info
    try {
      const jwt = await this.repo.exchangeYandexToken(token);
      if (!jwt) {
        throw new Error('No JWT received from repository');
      }
      console.log('AuthWithYandexUseCase: Token exchange successful'); // Validation: success
      return jwt;
    } catch (error) {
      console.error('AuthWithYandexUseCase: Error during exchange', error); // Logger: error
      throw error; // Передача ошибки вверх для обработки в UI
    }
  }
}