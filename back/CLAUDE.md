# CLAUDE.md — back/ (API .NET)

Détails techniques de l'API. Les règles métier et les principes de travail sont dans le `CLAUDE.md` racine.

## Organisation

```
back/
├── Onygo.slnx
├── dotnet-tools.json                 # outil local dotnet-ef
├── src/Onygo.Api/
│   ├── Program.cs                    # assemblage des services et des routes
│   ├── Auth/                         # configuration Identity, Bearer, politique de repli
│   └── Infrastructure/
│       ├── Persistence/              # OnygoDbContext et migrations EF Core
│       ├── Email/                    # implémentations de IEmailSender<IdentityUser>
│       └── OpenApi/                  # personnalisation du document OpenAPI
└── tests/
    ├── Onygo.Api.UnitTests/          # logique pure, sans base ni réseau
    └── Onygo.Api.IntegrationTests/   # HTTP → vrai PostgreSQL (Testcontainers) → réponse
        └── Infrastructure/           # OnygoApiFactory, FakeEmailSender, AuthApiClient
```

Les tests reprennent l'arborescence de `src/` (ex. `Infrastructure/Email/DevelopmentEmailSenderTests.cs`).

## Authentification

On utilise les **Identity API endpoints** de Microsoft (`MapIdentityApi`), sans surcouche maison, configurés dans `Auth/AuthSetup.cs`. Jetons Bearer **opaques** (pas de JWT) : accès 1 h, refresh 14 jours (valeurs par défaut).

**Bearer uniquement** : Identity est enregistré avec `AddIdentityCore` + `AddApiEndpoints` + `AddBearerToken`, sans les cookies qu'ajouterait `AddIdentityApiEndpoints`.

| Route | Usage |
|---|---|
| `POST /auth/register` | inscription (`email`, `password`), envoie le lien de confirmation |
| `GET /auth/confirmEmail` | lien reçu par e-mail |
| `POST /auth/resendConfirmationEmail` | renvoyer le lien |
| `POST /auth/login` | renvoie `accessToken`, `refreshToken`, `expiresIn` |
| `POST /auth/refresh` | nouveau couple de jetons à partir du `refreshToken` |
| `POST /auth/forgotPassword` | envoie un **code** de réinitialisation |
| `POST /auth/resetPassword` | `email`, `resetCode`, `newPassword` |
| `GET/POST /auth/manage/info` | infos du compte connecté |

Options : e-mail unique, **connexion refusée tant que l'e-mail n'est pas confirmé**, mot de passe de **8 caractères minimum** avec majuscule, minuscule, chiffre et caractère spécial (règles par défaut d'Identity, longueur relevée).

`UnsupportedIdentityFeaturesFilter` ferme ce que `MapIdentityApi` expose sans qu'on s'en serve :
- `?useCookies=true` ou `?useSessionCookies=true` sur `/auth/login` → **400**
- `/auth/manage/2fa` → **404** (2FA hors périmètre)

## Autorisation

**Politique de repli** : toute route exige un utilisateur connecté par défaut. Une route publique doit le dire explicitement avec `.AllowAnonymous()`. Une route inconnue appelée sans jeton répond donc 401, pas 404.

Routes publiques aujourd'hui : `/health`, les routes `/auth/*` sauf `/auth/manage/*`, et en développement `/openapi/v1.json` et Swagger UI. Côté `/auth`, c'est la convention `OpenAndRateLimitPublicRoute` (`Auth/AuthSetup.cs`) qui ouvre les routes sans exigence d'autorisation.

`UseAuthentication()` et `UseAuthorization()` sont appelés explicitement **après** `UseSwaggerUI()` : Swagger UI sert des fichiers via un middleware, pas via une route, et serait sinon bloqué.

## Limitation du nombre de requêtes

Sur les routes publiques `/auth/*`, par adresse IP (`Auth/AuthRateLimiting.cs`), réponse **429** au-delà :

| Quota | Routes | Défaut | Clé de configuration |
|---|---|---|---|
| connexion | login, register, refresh, resetPassword, confirmEmail | 20 / minute | `RateLimiting:Auth:SignInPermitsPerMinute` |
| e-mails | forgotPassword, resendConfirmationEmail | 5 / heure | `RateLimiting:Auth:EmailPermitsPerHour` |

- Valeurs larges exprès : tout un campus peut partager la même IP publique (Wi-Fi).
- **À faire avec l'hébergement** : configurer `ForwardedHeaders`, sinon l'API voit l'IP du proxy et tout le monde partage le même quota.
- Tests : `OnygoApiFactory` lève les limites (tous les tests ont la même IP) ; `RateLimitingTests` les abaisse avec `WithWebHostBuilder`.

## E-mails

Le fournisseur n'est pas encore choisi (décision ouverte). En attendant :
- **Development** : `DevelopmentEmailSender` écrit le lien ou le code dans la console, **sans l'adresse e-mail**
- **autres environnements** : `UnconfiguredEmailSender` lève une exception (pas de perte silencieuse)
- **tests d'intégration** : `FakeEmailSender` garde les liens et codes en mémoire

## Base de données

- Chaîne de connexion `ConnectionStrings:Onygo`. En local elle est dans `appsettings.Development.json` et pointe vers le PostgreSQL Docker (port **5433**). Hors local, elle passe par la variable d'environnement `ConnectionStrings__Onygo`, jamais par le dépôt.
- Les migrations ne sont **pas** appliquées au démarrage de l'API.
- Table `DataProtectionKeys` : clés qui chiffrent les jetons Bearer, stockées en base pour survivre aux redéploiements et être partagées entre instances. Elles y sont **en clair** : l'accès à la base doit rester restreint. Les chiffrer avec un certificat (`ProtectKeysWith…`) est à décider avec l'hébergement.

## Commandes (depuis `back/`)

```bash
dotnet tool restore                                   # installer dotnet-ef (une fois)
dotnet ef database update --project src/Onygo.Api     # appliquer les migrations en local
dotnet ef migrations add <Nom> --project src/Onygo.Api -o Infrastructure/Persistence/Migrations
dotnet run --project src/Onygo.Api                    # http://localhost:5100
dotnet test                                           # tous les tests (Docker requis)
dotnet format                                         # à lancer après chaque migration générée (fins de ligne)
dotnet format --verify-no-changes                     # vérification avant PR
```

En développement uniquement :
- **Swagger UI** sur http://localhost:5100/swagger : appeler `/auth/login`, copier l'`accessToken`, puis **Authorize** pour tester les routes protégées
- document OpenAPI sur `/openapi/v1.json` (généré par `Microsoft.AspNetCore.OpenApi`, source du client `@onygo/shared`) ; le schéma Bearer y est déclaré par `BearerSecuritySchemeTransformer`

## Tests d'intégration

- `OnygoApiFactory` démarre un conteneur `postgres:17-alpine`, applique les migrations et le partage avec toute la collection `Api` (`[Collection(ApiCollection.Name)]`).
- L'isolation passe par les données : chaque test crée ses propres utilisateurs avec `AuthApiClient.NewEmail()`.
- `AuthApiClient` appelle les routes comme le ferait un front (`RegisterConfirmedUserAsync`, `LoginConfirmedUserAsync`…).
