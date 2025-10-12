import { AuthRepository } from '../../core/ports/AuthRepository';
import { ApiClient } from '../api/ApiClient';

export class HttpAuthRepository implements AuthRepository {
  constructor(private api: ApiClient) {}

  async exchangeYandexToken(token: string): Promise<string> {
/*
Response object: 
{
    "jwt": null,
    "token": "..",
    "user": {
        "id": 1,
        "firstName": "",
        "username": "",
        "photoUrl": "https://"
    }
}
 */
    console.log('HttpAuthRepository: Exchanging Yandex token'); // Logger: info
    try {
      const response = await this.api.postYandexToken(token);
      if (!response) {
        throw new Error('Invalid response from API: no JWT');
      }
      console.log('HttpAuthRepository: Exchange successful'); // Validation: success
      return response.token;
    } catch (error) {
      console.error('HttpAuthRepository: Exchange failed', error); // Logger: error
      throw error;
    }
  }
}