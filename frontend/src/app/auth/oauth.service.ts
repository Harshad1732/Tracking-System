import { Injectable } from '@angular/core';
import { PublicClientApplication, AuthenticationResult } from '@azure/msal-browser';
import { environment } from '../../environments/environment';

declare global {
  interface Window {
    google?: any;
  }
}

@Injectable({ providedIn: 'root' })
export class OAuthService {
  private gisLoaded = false;
  private msal?: PublicClientApplication;

  configured = {
    google: !environment.googleClientId.startsWith('REPLACE_'),
    microsoft: !environment.microsoftClientId.startsWith('REPLACE_')
  };

  async loadGoogle(): Promise<void> {
    if (this.gisLoaded) return;
    await new Promise<void>((resolve, reject) => {
      if (document.getElementById('gis-script')) { resolve(); return; }
      const s = document.createElement('script');
      s.id = 'gis-script';
      s.src = 'https://accounts.google.com/gsi/client';
      s.async = true;
      s.defer = true;
      s.onload = () => resolve();
      s.onerror = () => reject(new Error('Failed to load Google Identity Services'));
      document.head.appendChild(s);
    });
    this.gisLoaded = true;
  }

  async renderGoogleButton(container: HTMLElement, onToken: (idToken: string) => void): Promise<void> {
    if (!this.configured.google) return;
    await this.loadGoogle();
    window.google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (resp: { credential: string }) => onToken(resp.credential)
    });
    window.google.accounts.id.renderButton(container, {
      theme: 'outline', size: 'large', width: 320, text: 'continue_with'
    });
  }

  private getMsal(): PublicClientApplication {
    if (!this.msal) {
      this.msal = new PublicClientApplication({
        auth: {
          clientId: environment.microsoftClientId,
          authority: environment.microsoftAuthority,
          redirectUri: window.location.origin
        },
        cache: { cacheLocation: 'sessionStorage' }
      });
    }
    return this.msal;
  }

  async microsoftSignIn(): Promise<string | null> {
    if (!this.configured.microsoft) return null;
    const pca = this.getMsal();
    await pca.initialize();
    const result: AuthenticationResult = await pca.loginPopup({
      scopes: ['openid', 'email', 'profile']
    });
    return result.idToken ?? null;
  }
}
