export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5217/api',
  googleClientId: 'REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com',
  microsoftClientId: 'REPLACE_WITH_MICROSOFT_APP_REGISTRATION_CLIENT_ID',
  microsoftAuthority: 'https://login.microsoftonline.com/common',

  // Dev bypass: when true, AuthService seeds a fake signed-in user so the app loads
  // straight into /dashboard without going through the login screen. Flip to false
  // (or remove) once real backend auth is being exercised.
  useDummyAuth: true
};
