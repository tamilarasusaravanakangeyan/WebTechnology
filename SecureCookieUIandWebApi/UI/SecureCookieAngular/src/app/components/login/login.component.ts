import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `
    <h2>Login</h2>
    <form (ngSubmit)="login()">
      <input type="text" [(ngModel)]="username" name="username" placeholder="Username" required />
      <input type="password" [(ngModel)]="password" name="password" placeholder="Password" required />
      <button type="submit">Login</button>
    </form>
    <p>{{ message }}</p>
  `,
  imports: [FormsModule],
})
export class LoginComponent {
  username = '';
  password = '';
  message = '';

  constructor(private authService: AuthService) {}

  login(): void {
    this.authService.login(this.username, this.password).subscribe({
      next: (data) => {
        this.message = data;
      },
      error: (err) => (this.message = err),
    });
  }
}
