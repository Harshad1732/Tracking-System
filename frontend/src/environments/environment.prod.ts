// Production environment — used when Angular is built with `ng build` (the default
// `--configuration production`). For dev, `environment.ts` is used instead.
//
// Set `apiBaseUrl` to the deployed backend's URL before deploying — e.g.:
//   apiBaseUrl: 'https://tracker-api.azurewebsites.net/api'
// If frontend and backend are served from the same origin (App Service hosting both,
// or via reverse proxy), a relative '/api' works too.
export const environment = {
  production: true,
  apiBaseUrl: '/api',
  googleClientId: 'REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com',
  microsoftClientId: 'REPLACE_WITH_MICROSOFT_APP_REGISTRATION_CLIENT_ID',
  microsoftAuthority: 'https://login.microsoftonline.com/common',

  // No dev bypass in production — the real login flow is the only way in.
  useDummyAuth: false
};
