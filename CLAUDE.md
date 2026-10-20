# CLAUDE.md — Onygo (racine)

Ce fichier donne le contexte global du projet : le produit, les règles métier, l'architecture et les règles de travail communes à tout le monorepo.
**Lis-le en entier avant toute modification du code.**

Chaque partie du projet a aussi son propre `CLAUDE.md` pour les détails techniques (structure, commandes, conventions spécifiques) :

| Fichier | Contenu |
|---|---|
| `back/CLAUDE.md` | API .NET : organisation, modèle de données, routes, authentification, tests back |
| `front/mobile/CLAUDE.md` | Appli Expo : navigation, stockage des jetons, modules, builds |
| `front/web/CLAUDE.md` | Site Next.js : pages, référencement |
| `front/shared/CLAUDE.md` | Paquet commun : génération du client API |

**Règle anti-doublon** : une information vit dans un seul fichier. Les règles métier et les principes de travail sont **ici** ; les détails techniques sont dans les fichiers des dossiers. En cas de contradiction entre un fichier et le code, signale l'écart au lieu de deviner.

---

## 1. Le projet

**Onygo** (onygo.app) est une appli de rencontre où l'on swipe des **activités** plutôt que des personnes.
Quelqu'un propose une sortie, d'autres postulent, et l'organisateur choisit avec qui il y va.
Le premier date est organisé dès le match.

- **Boutons du feed** : **O** (oui, je postule) et **X** (non, je passe) — mêmes boutons pour l'organisateur quand il trie les candidats
- **Cible de lancement** : étudiants de 18 à 25 ans à Nantes, en commençant par un seul campus
- **Langue de l'interface** : français
- **Livrables** : une appli mobile (iOS et Android), un site web, une API

### Principes produit

1. **L'activité d'abord, la personne ensuite.** Visuellement et dans le parcours, l'activité passe avant le profil.
2. **Transformer les matchs en vraies sorties.** C'est l'indicateur qui compte, pas le nombre de swipes.
3. **Simplicité avant peaufinage.** Une version simple et utilisée vaut mieux qu'une version parfaite.
4. **Sécurité et respect.** Pas de rejet frontal, signalement facile, données personnelles protégées.

---

## 2. Périmètre fonctionnel

### 2.1 MVP : mode Duo uniquement

Une activité réunit **un organisateur et une seule personne acceptée**.

### 2.2 Parcours utilisateur

1. **Inscription** : e-mail et mot de passe, confirmation de l'e-mail
2. **Profil** : prénom, date de naissance (18+ obligatoire), 1 à 3 photos, au moins un contact (Instagram ou téléphone), courte bio facultative
3. **Création d'activité** : type, titre, texte d'accroche obligatoire, lieu, date et heure
4. **Feed** : les autres utilisateurs parcourent les activités et appuient sur **O** pour postuler (petit mot facultatif) ou sur **X** pour passer
5. **Sélection** : l'organisateur voit les candidatures et appuie sur **O** pour accepter ou **X** pour refuser
6. **Match** : dès qu'un candidat est accepté, l'activité est pourvue et les deux personnes sont mises en relation
7. **Avant la sortie** (MVP) : confirmation et rappel

### 2.3 La carte d'activité

- **Au premier plan** : titre, type, lieu, date et heure, image, texte d'accroche
- **En secondaire** : photos de l'organisateur (vignettes ou révélées au tap — à tester), prénom et âge

### 2.4 Règles métier des candidatures

Ces règles sont **implémentées et garanties par l'API**, protégées par des **contraintes en base** quand c'est possible. Le front les affiche mais ne les garantit **jamais**. Chaque règle doit avoir **ses tests** (voir §8).

| # | Règle | Détail |
|---|---|---|
| R1 | **3 candidatures en attente maximum** par activité | Au-delà, l'activité n'apparaît plus dans le feed et refuse les nouvelles candidatures |
| R2 | **Un refus libère une place** | L'activité revient dans le feed si elle est encore ouverte |
| R3 | **Pas de re-candidature** | Une seule candidature par personne et par activité, quel que soit son statut ; un refusé ne revoit plus l'activité |
| R4 | **Une acceptation pourvoit l'activité** | L'activité passe en `Filled`, toutes les autres candidatures `Pending` passent en `Expired` |
| R5 | **Expiration automatique** | Les candidatures `Pending` expirent avant la date de l'activité ; une activité passée sans acceptation passe en `Expired` |
| R6 | **5 candidatures maximum par utilisateur et par jour** | Jour calendaire en heure de Paris (`Europe/Paris`) |
| R7 | **On ne postule pas à sa propre activité** | |
| R8 | **On ne postule qu'à une activité `Open` et future** | |
| R9 | **Refus silencieux** | Aucun message ni notification de refus, comme sur Tinder ou Hinge : le candidat ne revoit simplement plus l'activité. L'API ne lui expose **jamais** le statut `Refused` ; ses candidatures refusées ou expirées n'apparaissent plus dans « Mes candidatures » |
| R10 | **Aucune suppression physique des candidatures** | Statuts : `Pending`, `Accepted`, `Refused`, `Expired` |
| R11 | **Alerte délai court** | Si on postule à une activité qui commence **dans moins de 6 h**, ou si l'organisateur accepte une candidature **moins de 6 h** avant le début, l'appli affiche une petite alerte : « Attention, l'autre personne risque de ne pas le voir à temps ». L'action reste possible ; l'alerte informe, elle ne bloque pas |

Les limites (3 en attente, 5 par jour, 6 h) sont des **constantes métier** définies côté API et reprises dans `@onygo/shared/constants` pour l'affichage.

R1 et R4 sont **sensibles à la concurrence** (deux requêtes simultanées) : elles doivent être protégées par une transaction et un verrou, et testées comme telles.

### 2.5 Feed

Le feed renvoie les activités :
- au statut `Open` et dont la date est future
- dont l'utilisateur n'est pas l'organisateur
- auxquelles il n'a pas déjà postulé
- avec moins de 3 candidatures `Pending`
- (MVP) sans blocage entre l'utilisateur et l'organisateur

Les activités qui commencent dans moins de 6 h restent visibles, avec l'indicateur d'alerte de R11.

Tri : date la plus proche d'abord. Pagination par curseur.

### 2.6 Sécurité et légal (obligatoire avant toute publication sur les stores)

- Signalement et blocage d'un utilisateur (exigé par l'App Store)
- Modération des signalements par l'équipe
- RGPD : données hébergées dans l'UE, consentement à la localisation, export et suppression du compte sur demande, politique de confidentialité
- CGU avec interdiction aux mineurs, vérification 18+ à l'inscription
- Conseils de rendez-vous dans des lieux publics
- Le contact d'un utilisateur n'est **jamais** visible en dehors d'un match

### 2.7 Hors périmètre

Ne pas développer sans validation explicite de l'équipe : mode Groupe, partenariats avec des lieux, système de réputation, offre premium, paiements, connexion Google ou Apple, JWT.

---

## 3. Feuille de route

### V0 — version testable par l'entourage

- [ ] Monorepo, Docker, API et appli qui démarrent
- [ ] Authentification : inscription, confirmation e-mail, connexion, refresh, mot de passe oublié
- [ ] Profil et envoi des photos
- [ ] Créer une activité
- [ ] Feed simple avec les boutons O et X (pas de swipe animé)
- [ ] Postuler, accepter, refuser (O / X), refus silencieux, alerte délai court
- [ ] Écran de match affichant le contact de l'autre personne (pas de chat)
- [ ] Expiration automatique
- [ ] Landing page avec liste d'attente, pages de confirmation e-mail et de réinitialisation

### MVP — après les premiers retours

- [ ] Chat en temps réel (SignalR)
- [ ] Swipe animé
- [ ] Confirmation et rappels avant la sortie
- [ ] Notifications push
- [ ] Signalement, blocage, modération
- [ ] Filtre par distance
- [ ] Export et suppression du compte (RGPD)

---

## 4. Architecture

### 4.1 Vue d'ensemble

```
┌─────────────────┐   ┌─────────────────┐
│  front/mobile   │   │    front/web    │
│  Expo / RN      │   │    Next.js      │
└────────┬────────┘   └────────┬────────┘
         │   front/shared (types + client API générés)
         └───────────┬─────────┘
                     │ HTTPS + jeton Bearer
              ┌──────▼──────┐        ┌──────────────────────┐
              │    back/    │───────▶│ Stockage objet (S3)  │
              │  API .NET   │        │ photos, URL signées  │
              └──────┬──────┘        └──────────────────────┘
                     │ EF Core       ┌──────────────────────┐
              ┌──────▼──────┐        │ Fournisseur e-mail   │
              │ PostgreSQL  │        └──────────────────────┘
              └─────────────┘
```

### 4.2 Stack

| Couche | Choix |
|---|---|
| Appli mobile | React Native + **Expo**, TypeScript strict, Expo Router |
| Site web | **Next.js**, TypeScript strict |
| Code partagé | `@onygo/shared` : types et client API générés depuis l'OpenAPI de l'API |
| API | **.NET 10**, Minimal API, C# |
| Authentification | ASP.NET Core Identity + Identity API endpoints, jetons Bearer opaques + refresh tokens |
| Base de données | **PostgreSQL** + Entity Framework Core (Npgsql) |
| Photos | Stockage objet compatible S3, envoi par URL signées |
| E-mails | Fournisseur externe derrière l'interface `IEmailSender` |
| Builds mobile | EAS Build, Expo Go en dev, TestFlight pour la bêta |

### 4.3 Décisions ouvertes

Ne pas les trancher dans le code sans validation de l'équipe ; passer par des interfaces pour garder le choix ouvert.

- Fournisseur d'e-mails (Brevo ou Resend)
- Hébergement de l'API et de PostgreSQL pour test et prod (**région UE obligatoire**)
- Stockage des photos (Cloudflare R2 ou Supabase Storage)
- Affichage des photos sur la carte d'activité (test utilisateur)

---

## 5. Arborescence du monorepo

```
onygo/
├── CLAUDE.md                 # ce fichier
├── docker-compose.yml        # PostgreSQL local
├── package.json              # workspaces npm : front/*
├── .gitignore
├── .editorconfig
├── back/                     # API .NET          → voir back/CLAUDE.md
│   ├── Onygo.sln
│   ├── src/Onygo.Api/
│   └── tests/
│       ├── Onygo.Api.UnitTests/
│       └── Onygo.Api.IntegrationTests/
└── front/
    ├── mobile/               # appli Expo        → voir front/mobile/CLAUDE.md
    ├── web/                  # site Next.js      → voir front/web/CLAUDE.md
    └── shared/               # @onygo/shared     → voir front/shared/CLAUDE.md
```

---

## 6. Environnements

| Environnement | Base | API | Usage |
|---|---|---|---|
| **local** | Docker (`docker compose up -d`) | sur la machine | développement quotidien |
| **test** | hébergée, UE | hébergée, UE | bêta : TestFlight, entourage |
| **prod** | hébergée, UE | hébergée, UE | vrais utilisateurs |

**Circuit** : local → pull request → déploiement sur **test** → validation → déploiement sur **prod**.

- Aucune modification manuelle des bases de test ou de prod : tout passe par les migrations.
- Aucun secret dans le dépôt : `.env` et secrets jamais commités, un `.env.example` à jour dans chaque dossier qui en a besoin.

---

## 7. Clean code

Le code doit être **lisible par un autre membre de l'équipe sans explication**. Ces principes s'appliquent au back comme au front.

### 7.1 Lisibilité

- **Des noms qui disent l'intention** : `pendingApplicationsCount`, pas `cnt` ; `CanApply()`, pas `Check()`. Un nom qui demande un commentaire est un mauvais nom.
- **Des fonctions courtes qui font une seule chose.** Si une fonction a besoin de sous-titres internes, découpe-la.
- **Peu de paramètres** : au-delà de 3, regrouper dans un objet.
- **Retours anticipés** plutôt que des `if` imbriqués.
- **Pas de nombres ou de chaînes magiques** : les limites métier (3, 5, 6 h) et les statuts sont des constantes nommées, définies à un seul endroit par couche.
- **Commenter le pourquoi, jamais le quoi.** Un commentaire qui paraphrase le code est à supprimer. Un commentaire qui explique une décision non évidente (une règle métier, un contournement) est bienvenu.

### 7.2 Conception

- **Une responsabilité par classe, composant ou module.** Un écran n'appelle pas l'API directement s'il peut passer par un hook ; un handler ne formate pas des e-mails.
- **La logique métier est isolée et testable** : elle ne dépend ni d'HTTP, ni de l'interface, ni de l'heure système.
- **Dépendances injectées**, pas instanciées en dur (services externes, horloge, stockage, e-mail).
- **Pas d'abstraction prématurée (YAGNI).** Pas d'interface avec une seule implémentation, sauf pour les frontières externes (e-mail, stockage, horloge) ou pour les tests. Pas de couche générique « au cas où ».
- **Duplication** : la tolérer une fois, factoriser à la troisième occurrence, et seulement si les cas évoluent vraiment ensemble.
- **Erreurs explicites** : pas d'exceptions avalées, pas de `catch` vide. Les erreurs métier ont un code clair, traduit côté front.
- **Immutabilité par défaut** côté TypeScript (`const`, pas de mutation d'objets partagés) ; `record` et propriétés `init` côté C# pour les DTO.

### 7.3 Hygiène

- **Pas de code mort**, pas de code commenté, pas de `console.log` ni de `Console.WriteLine` de débogage laissés dans une PR.
- **Pas de `TODO` sans contexte** : un `TODO` indique pourquoi et, idéalement, renvoie à une tâche.
- **Laisser le code un peu plus propre qu'on ne l'a trouvé**, sans transformer une PR de fonctionnalité en grand refactoring : un refactoring important fait l'objet de sa propre PR.
- **Rester cohérent avec le code existant** : avant d'écrire, regarder comment le reste du projet fait la même chose.
- **Les outils font foi** : formateurs et linters (Prettier, ESLint, `dotnet format`) sans avertissement avant toute PR.

---

## 8. Tests

### 8.1 Règle d'or

**Pas de fonctionnalité sans tests.** Toute fonctionnalité ajoutée ou modifiée arrive dans la **même PR** que ses tests. Toute correction de bug commence par **un test qui reproduit le bug**, puis le corrige.

### 8.2 Quels tests écrire

- **Tests unitaires** pour la logique pure : règles métier, validations, calculs (âge, limite journalière, statuts), fonctions utilitaires, hooks et formatages côté front. Rapides, sans base de données ni réseau.
- **Tests d'intégration dès que c'est possible** pour tout ce qui traverse plusieurs couches : routes de l'API de bout en bout (requête HTTP → base PostgreSQL réelle → réponse), authentification et autorisations, contraintes en base, concurrence (R1, R4).
- **Tests de composants** côté front pour les écrans et composants qui portent du comportement (affichage des états, actions de l'utilisateur), pas pour du pur affichage statique.
- **Tests de bout en bout** (parcours complets sur l'appli) : hors V0, à envisager plus tard.

Les règles métier **R1 à R11** doivent toutes être couvertes, au minimum par un test d'intégration côté API.

### 8.3 Des tests simples et maintenables

- **Un test vérifie un seul comportement.** Plusieurs assertions sont acceptables si elles décrivent le même comportement.
- **Structure Arrange / Act / Assert** visible, séparée par une ligne vide.
- **Des noms qui décrivent le comportement attendu**, en anglais comme le code, par exemple :
  - C# : `Apply_WhenActivityHas3PendingApplications_ReturnsActivityFull`
  - TypeScript : `it("shows the short-notice warning when the activity starts in less than 6 hours")`
- **Tester le comportement, pas l'implémentation** : on vérifie ce qu'une route renvoie ou ce que l'écran affiche, pas quelle méthode privée a été appelée. Un refactoring sans changement de comportement ne doit casser aucun test.
- **Des données de test lisibles** : des builders ou fabriques (`ActivityBuilder`, `aUser()`) avec des valeurs par défaut valides, où chaque test ne précise que ce qui compte pour lui.
- **Des tests indépendants** : chaque test prépare ses propres données, aucun ordre d'exécution imposé, aucun état partagé modifiable.
- **Des tests déterministes** : heure contrôlée (horloge injectée et simulée), pas de `sleep`, pas d'aléatoire non maîtrisé, pas d'appel réseau réel.
- **Mocker uniquement les frontières externes** : e-mail, stockage des photos, horloge, services tiers. **Ne pas mocker la base de données** dans les tests d'intégration : utiliser un vrai PostgreSQL (Testcontainers).
- **Pas de logique dans les tests** (pas de boucles ni de conditions complexes) : un test doit se lire comme une spécification.
- **Un test fragile ou lent se corrige**, il ne se désactive pas. Un test ignoré doit indiquer pourquoi.

### 8.4 Outils

| Partie | Unitaires | Intégration / composants |
|---|---|---|
| `back/` | xUnit | xUnit + `WebApplicationFactory` + **Testcontainers** (PostgreSQL) |
| `front/mobile/` | Jest (`jest-expo`) | React Native Testing Library |
| `front/web/` | Jest ou Vitest | React Testing Library |
| `front/shared/` | Jest ou Vitest | — |

Les détails (organisation des dossiers, fixtures, commandes) sont dans le `CLAUDE.md` de chaque partie.

### 8.5 Pas d'objectif de couverture en pourcentage

On ne vise pas un chiffre. On vise : toutes les règles métier testées, tous les chemins d'erreur importants testés (non authentifié, non autorisé, données invalides, limites atteintes), et chaque bug corrigé protégé par un test de non-régression.

---

## 9. Définition de « terminé »

Une fonctionnalité est terminée quand :

- [ ] elle respecte les règles métier et le périmètre de ce fichier
- [ ] ses **tests unitaires** et, dès que possible, ses **tests d'intégration** sont écrits et passent
- [ ] **tous** les tests du projet passent
- [ ] formateurs et linters ne remontent aucun avertissement
- [ ] le client `@onygo/shared` est régénéré si l'API a changé, et les deux fronts compilent
- [ ] une migration est créée si le modèle de données a changé
- [ ] aucun secret, aucune donnée personnelle dans le code ou les logs
- [ ] la PR est relue par un autre membre de l'équipe
- [ ] la documentation (`CLAUDE.md` concernés) est à jour si une décision a changé

---

## 10. Conventions communes

### 10.1 Langues

- **Code** (classes, variables, routes, tables, noms de tests) : anglais
- **Interface utilisateur** : français
- **Documentation, commits, PR, commentaires** : français

### 10.2 Git

- `main` protégée : tout passe par une **pull request** relue
- Branches : `feat/…`, `fix/…`, `chore/…`, `refactor/…`, `test/…`
- Commits courts et explicites, en français : `feat: limite de 3 candidatures en attente par activité`
- Une PR = un sujet. Les PR courtes sont relues plus vite et mieux.

### 10.3 Glossaire

| Français | Code |
|---|---|
| Activité | `Activity` |
| Organisateur | `Organizer` |
| Candidature | `ActivityApplication` |
| Candidat | `Applicant` |
| Accroche | `Hook` |
| Ouverte / pourvue / expirée / annulée | `Open` / `Filled` / `Expired` / `Cancelled` |
| En attente / acceptée / refusée / expirée | `Pending` / `Accepted` / `Refused` / `Expired` |
| Match | candidature `Accepted` |
| Boutons O / X | `Yes` / `No` (postuler ou passer ; accepter ou refuser) |
| Alerte délai court | `ShortNoticeWarning` (moins de 6 h avant le début) |

---

## 11. Commandes de base (racine)

```bash
docker compose up -d        # lancer PostgreSQL local
docker compose down         # l'arrêter
npm install                 # installer les dépendances de tous les fronts (workspaces)
```

Les commandes propres à chaque partie sont dans son `CLAUDE.md`.

---

## 12. Instructions pour Claude

### À faire

- Lire ce fichier, le `CLAUDE.md` de la partie concernée et le code existant avant de proposer du code
- Respecter les principes de clean code (§7) et la politique de tests (§8)
- **Écrire les tests en même temps que la fonctionnalité**, dans la même modification : unitaires pour la logique, intégration dès que c'est possible
- Pour un bug : écrire d'abord le test qui le reproduit
- Lancer les tests et les linters, et vérifier qu'ils passent avant de considérer une tâche comme terminée
- Proposer des changements petits et ciblés, faciles à relire
- Après une modification de l'API : régénérer `@onygo/shared` et corriger les fronts
- Signaler tout risque de sécurité, de fuite de données personnelles ou de non-conformité RGPD
- Mettre à jour les `CLAUDE.md` concernés quand une décision structurante change

### À ne pas faire

- Livrer une fonctionnalité sans ses tests, ou supprimer, désactiver ou affaiblir un test pour le faire passer
- Ajouter une dépendance sans la signaler et la justifier
- Développer une fonctionnalité hors périmètre (§2.7) ou trancher une décision ouverte (§4.3)
- Complexifier l'architecture (nouvelles couches, outils, services) sans demande explicite
- Mélanger refactoring important et nouvelle fonctionnalité dans la même modification
- Mettre de la logique métier critique uniquement dans le front
- Exposer le contact d'un utilisateur en dehors d'un match
- Mettre des données personnelles dans les logs

---

## 13. Indicateurs de succès

- **Part des matchs qui deviennent une vraie sortie** (indicateur clé)
- Taux de lapins
- Rétention à 7 et 30 jours
- Nombre d'activités créées par semaine sur le campus
- Délai moyen de réponse de l'organisateur aux candidatures
