import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-logout',
  standalone: true,
  template: `<button (click)="logout()">Logout</button>`,
})
export class LogoutComponent {
  constructor(private authService: AuthService) {}

  logout(): void {
    this.authService.logout().subscribe({
      next: (response) => console.log(response),
      error: (err) => console.error(err),
    });
  }
}
