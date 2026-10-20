# CLAUDE.md — back/ (API .NET)

Détails techniques de l'API. Les règles métier et les principes de travail sont dans le `CLAUDE.md` racine.

## Organisation

```
back/
├── Onygo.slnx
├── dotnet-tools.json                 # outil local dotnet-ef
├── src/Onygo.Api/
│   ├── Program.cs                    # configuration des services et des routes
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

On utilise les **Identity API endpoints** de Microsoft (`AddIdentityApiEndpoints` + `MapIdentityApi`), sans surcouche maison. Jetons Bearer **opaques** (pas de JWT) : accès 1 h, refresh 14 jours (valeurs par défaut).

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

Options : e-mail unique, **connexion refusée tant que l'e-mail n'est pas confirmé**, politique de mot de passe par défaut d'Identity (6 caractères minimum, majuscule, minuscule, chiffre, caractère spécial).

Les routes `/auth/manage/2fa` sont aussi exposées par `MapIdentityApi`, mais la 2FA ne fait pas partie du périmètre.

## E-mails

Le fournisseur n'est pas encore choisi (décision ouverte). En attendant :
- **Development** : `DevelopmentEmailSender` écrit le lien ou le code dans la console, **sans l'adresse e-mail**
- **autres environnements** : `UnconfiguredEmailSender` lève une exception (pas de perte silencieuse)
- **tests d'intégration** : `FakeEmailSender` garde les liens et codes en mémoire

## Base de données

- Chaîne de connexion `ConnectionStrings:Onygo`. En local elle est dans `appsettings.Development.json` et pointe vers le PostgreSQL Docker (port **5433**). Hors local, elle passe par la variable d'environnement `ConnectionStrings__Onygo`, jamais par le dépôt.
- Les migrations ne sont **pas** appliquées au démarrage de l'API.

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
