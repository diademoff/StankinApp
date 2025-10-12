import { AuthRepository } from '../../core/ports/AuthRepository';
import { ApiClient } from '../api/ApiClient';

export class HttpAuthRepository implements AuthRepository {
  constructor(private api: ApiClient) {}

  async exchangeYandexToken(token: string): Promise<string> {
    console.log('HttpAuthRepository: Exchanging Yandex token'); // Logger: info
    try {
      const response = await this.api.postYandexToken(token);
      if (!response || !response.jwt) {
        throw new Error('Invalid response from API: no JWT');
      }
      console.log('HttpAuthRepository: Exchange successful'); // Validation: success
      return response.jwt;
    } catch (error) {
      console.error('HttpAuthRepository: Exchange failed', error); // Logger: error
      throw error;
    }
  }
}