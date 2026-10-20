# Suivi du projet — où on en est, quoi faire ensuite

Fichier de reprise : à lire en début de session, avec `CLAUDE.md` (règles métier et principes) et `back/CLAUDE.md` (détails techniques de l'API).
Il ne répète pas les règles : il dit **ce qui est fait, ce qui reste, dans quel ordre, et ce qui attend une décision**.

> Dernière mise à jour : 20 octobre 2026 — feed et expiration (R5) fusionnés dans `main`.

---

## 1. Reprendre en 2 minutes

```bash
docker compose up -d                                   # PostgreSQL local, port 5433
cd back
dotnet tool restore                                    # dotnet-ef
dotnet ef database update --project src/Onygo.Api      # appliquer les migrations
dotnet run --project src/Onygo.Api                     # http://localhost:5100/swagger
dotnet test                                            # 193 tests, Docker requis
```

- Si l'API tourne déjà pendant `dotnet test`, le build échoue (fichiers verrouillés) : lancer `dotnet test -c Release`.
- En développement, les liens de confirmation d'e-mail et codes de réinitialisation s'affichent dans la console de l'API.
- Pour essayer à la main dans Swagger : `/auth/register` → lien de confirmation dans la console → `/auth/login` → **Authorize** avec l'`accessToken` → `POST /profile` → `POST /activities`.

---

## 2. Ce qui est fait (API uniquement, rien côté front)

| Bloc | Contenu |
|---|---|
| Monorepo | `docker-compose.yml` (PostgreSQL 17), workspaces npm, `.editorconfig`, `.gitattributes` (LF) |
| Authentification | Identity API endpoints sous `/auth` : inscription, confirmation e-mail, connexion, refresh, mot de passe oublié. Bearer seul (pas de cookie), 2FA fermée |
| Sécurité | authentification exigée par défaut (politique de repli), limitation des requêtes sur `/auth`, clés de jetons en base, mot de passe 8 caractères, Swagger en dev seulement |
| Profil | `GET/POST/PATCH /profile` : prénom, date de naissance (18+, non modifiable) |
| Activités | `POST /activities`, `GET /activities/{id}` : types fixes ou « autre », 30 jours max, lieu exact caché derrière un cercle de 5 km décalé |
| Candidatures | postuler, trier (O / X), « mes candidatures » : R1 à R4 et R6 à R11, verrous contre la concurrence |
| Feed | `GET /activities/feed` : critères du §2.5, tri par date, pagination par curseur, alerte délai court (R11) |
| Expiration (R5) | 1 h avant le début : candidatures en attente et activités sans acceptation expirent (tâche de fond chaque minute) ; candidatures fermées, création au moins 1 h à l'avance |

---

## 3. Prochaines étapes, dans l'ordre proposé

Chaque étape = une branche `feat/…`, tests écrits avec le code, fusion dans `main` après validation.

### ~~Étape 1 — Feed~~ ✅ fait, fusionné dans `main`

Reste hors étape : blocage entre utilisateurs (MVP), filtre par distance (MVP).

### ~~Étape 2 — Expiration automatique (R5)~~ ✅ fait, fusionné dans `main`

Décision prise : expiration **1 h avant le début** (règles mises à jour dans `CLAUDE.md` §2.3, §2.4, §2.5).
Reste à préciser avec le front : comment l'organisateur voit une activité expirée sans personne.

### Étape 3 — Compléter le profil (`feat/profile-contact`)

- [ ] Bio courte facultative (longueur max à fixer)
- [ ] Contact : Instagram et/ou téléphone, **au moins un** obligatoire (création du profil ou étape séparée ? à décider)
- [ ] Validation : format du pseudo Instagram, format du téléphone (FR seulement ?)
- [ ] Le contact n'est **jamais** renvoyé hors d'un match (`CLAUDE.md` §2.6) : test dédié qui vérifie la réponse brute de chaque route qui expose un profil
- [ ] `PATCH /profile` : bio et contact modifiables

### Étape 4 — Écran de match (`feat/match`)

- [ ] Route qui renvoie, pour une candidature `Accepted`, le contact de l'autre personne et le lieu exact — accessible aux deux personnes du match seulement
- [ ] Liste « mes matchs » côté organisateur et côté candidat (ou intégré à `/applications/mine` + activités organisées)
- [ ] Tests : un tiers, un refusé, un expiré n'y ont pas accès

### Étape 5 — Photos (`feat/photos`) — attend une décision

- [ ] **Décision** : Cloudflare R2 ou Supabase Storage (région UE)
- [ ] Interface de stockage (frontière externe → interface autorisée), envoi par URL signées
- [ ] 1 à 3 photos par profil ; image de la carte d'activité
- [ ] Tests avec un faux stockage

### Étape 6 — Fronts

- [ ] `front/shared` : génération des types et du client depuis `/openapi/v1.json`, constantes métier (3, 5, 6 h, 18 ans, longueurs max)
- [ ] `front/mobile` (Expo) : auth, profil, création d'activité, feed O / X, tri des candidatures, alerte délai court, écran de match
- [ ] `front/web` (Next.js) : landing + liste d'attente, pages de confirmation d'e-mail et de réinitialisation du mot de passe
- [ ] Les `CLAUDE.md` de chaque dossier front
- [ ] Côté API : CORS pour le site web ; décider si le lien de confirmation pointe vers le site plutôt que vers l'API

### Étape 7 — Premier déploiement (environnement de test)

- [ ] **Décisions** : hébergeur API + PostgreSQL (UE), fournisseur d'e-mails (Brevo ou Resend)
- [ ] Brancher le fournisseur d'e-mails (remplace `UnconfiguredEmailSender`)
- [ ] `ForwardedHeaders` (sinon la limitation des requêtes voit l'IP du proxy), HTTPS / HSTS
- [ ] Chiffrer les clés de jetons au repos (`ProtectKeysWith…`) ou restreindre l'accès à la base
- [ ] Appliquer les migrations au déploiement (jamais à la main)
- [ ] Secrets par variables d'environnement, `.env.example` à jour

### Ensuite — MVP (`CLAUDE.md` §3)

Chat (SignalR), swipe animé, confirmation et rappels avant la sortie, notifications push, **signalement / blocage / modération** et **suppression de compte** (obligatoires pour l'App Store), export RGPD, filtre par distance, annulation et modification d'une activité (statut `Cancelled` déjà prévu).

---

## 4. Décisions en attente

| Sujet | Options | Bloque |
|---|---|---|
| Fournisseur d'e-mails | Brevo / Resend | tout environnement hors local |
| Hébergement API + base | à choisir, UE obligatoire | déploiement |
| Stockage des photos | Cloudflare R2 / Supabase Storage | photos |
| Contact | obligatoire à la création du profil ou plus tard ; formats acceptés | étape 3 |
| Suppression de compte | que deviennent activités et candidatures (R10, pas de cascade aujourd'hui) | suppression de compte |
| Contrôleurs ou Minimal API | Minimal API retenu (`CLAUDE.md` §4.2) ; à rediscuter en équipe si besoin | — |

---

## 5. Points connus à garder en tête

- **Double création de profil simultanée** : la seconde reçoit une 500 au lieu d'une 409 (la base empêche bien le doublon).
- **Inscription** : `/auth/register` révèle si un e-mail est déjà inscrit — accepté pour la V0.
- **Pas de vraie déconnexion** côté serveur : un jeton d'accès reste valable 1 h au plus.
- **Migrations générées** en CRLF : lancer `dotnet format` après chaque `dotnet ef migrations add`.
- **Tests d'intégration** : 25 à 50 s ; l'horloge est figée au 15 juin 2026, 10 h à Paris et partagée → ne jamais l'avancer, modifier la base avec `factory.WithDatabaseAsync`.
- **Pas de CLI GitHub (`gh`)** installée : les fusions ont été faites en local (`git merge --no-ff`) puis poussées. L'installer permettrait de passer par de vraies PR.
- **Branches déjà fusionnées** encore présentes en local et sur GitHub : à supprimer quand tu veux.

---

## 6. Façon de travailler retenue

- Une étape à la fois, validée avant de passer à la suivante.
- Une branche par sujet, partie de `main` à jour ; commit en français ; fusion `--no-ff` puis push après ton accord.
- Test qui échoue d'abord, puis le code ; pour les règles sensibles (âge, lieu exact, concurrence), on casse volontairement le code pour vérifier que les tests le détectent.
- Avant chaque commit : `dotnet format --verify-no-changes`, build sans avertissement, tous les tests verts.
- Toute nouvelle dépendance est signalée et justifiée ; les décisions ouvertes ne sont pas tranchées dans le code.
