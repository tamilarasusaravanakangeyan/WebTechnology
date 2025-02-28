import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LoginComponent } from "./components/login/login.component";
import { LogoutComponent } from "./components/logout/logout.component";

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, LoginComponent, LogoutComponent],
  template:`
  <h1>Welcome</h1>
  <app-login>asas</app-login>
  <app-logout></app-logout>
`,
  styleUrl: './app.component.less'
})
export class AppComponent {
  title = 'SecureCookieAngular';
}
