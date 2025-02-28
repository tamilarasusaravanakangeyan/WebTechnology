import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private baseUrl = 'https://localhost:44322/api/auth';

  constructor(private http: HttpClient) {}

  login(username: string, password: string): Observable<string> {
    return this.http.post<string>(
      `${this.baseUrl}/login`,
      { username, password },
      { withCredentials: true } // Important: Enables cookies
    );
  }

  logout(): Observable<string> {
    return this.http.post<string>(
      `${this.baseUrl}/logout`,
      {},
      { withCredentials: true }
    );
  }
}
