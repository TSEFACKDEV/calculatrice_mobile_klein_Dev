# Calculatrice — `</Klein_Dev>`

Application mobile **multi-plateforme** de calculatrice scientifique, écrite en **C# avec .NET MAUI 10**.
Un seul projet, quatre modes de calcul (standard, scientifique, programmeur, convertisseur d'unités),
un historique horodaté et une interface sombre pensée pour le mobile.

---

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Stack technique](#stack-technique)
- [Prérequis](#prérequis)
- [Démarrage rapide](#démarrage-rapide)
- [Exécuter sur chaque OS](#exécuter-sur-chaque-os)
- [Tester l'application](#tester-lapplication)
- [Structure du projet](#structure-du-projet)
- [Dépannage](#dépannage)

---

## Fonctionnalités

### Mode Standard

Arithmétique complète avec gestion correcte des priorités, des parenthèses auto-fermantes et de l'affichage en direct de l'aperçu du résultat.

| Touche | Effet |
|---|---|
| `+ − × ÷` | Opérations de base |
| `x^` | Puissance |
| `%` | Pourcentage |
| `±` | Changement de signe |
| `( )` | Groupement, fermeture automatique |
| `√` `x²` `x³` `1/x` | Racine, puissances, inverse |
| `x!` | Factorielle |
| `MS MR M+ M− MC` | Registre mémoire (5 emplacements) |
| `⌫` (retour arrière) `AC` (tout effacer) | Correction / tout effacer |

### Mode Scientifique

Trigonométrie, logarithmes, constantes et recalled du dernier résultat.

- `sin` `cos` `tan` et inverses `asin` `acos` `atan`
- `ln` `log` (base 10) `log₂` (base 2), `√`
- Constantes : `π`, `e`
- `Ans` — rappel du dernier résultat calculé
- Bascule d'angle en un tap : **DEG puis RAD puis GRAD** (`MainPage.xaml.cs:626`)
- Exposants `xʸ`

### Mode Programmeur

Calcul en bases multiples et opérations bit à bit.

- Bases **HEX / DEC / OCT / BIN** avec reformatage instantané du résultat (`Services/CalculatorEngine.cs:487`)
- Clavier `A`–`F` en hexadécimal
- Opérations logiques : `AND` `OR` `XOR` `NOT`
- `MOD`, décalages `<<` et `>>`

### Convertisseur d'unités

Onze catégories, conversion dans les deux sens avec table deéquivalence générée automatiquement.

`Longueur` · `Masse` · `Température` · `Aire` · `Volume` · `Vitesse` · `Temps` · `Données` · `Angle` · `Énergie` · `Puissance`

### Commun à tous les modes

- **Historique** des 60 derniers calculs (expression, résultat, horodage), réinjectable en un tap
- **Aperçu live** du résultat pendant la saisie
- **Gestion d'erreurs explicite** (division par zéro, parenthèses non fermées, expressions invalides)

---

## Stack technique

| Élément | Valeur |
|---|---|
| Framework | .NET 10 / .NET MAUI 10 |
| Langage | C# 14 |
| UI | XAML (`.xaml`) + code-behind |
| Moteur de calcul | Analyseur syntaxique maison (descendant récursif), sans dépendance externe |
| Cible principale | `net10.0-android` (Android API 21+) |
| Cibles secondaires | `net10.0-ios`, `net10.0-maccatalyst` (iOS 15+), `net10.0-windows10.0.19041.0` |
| Version | 1.0 |

> **Note** : le fichier `Calculatrice.csproj` est *conditionnel*. Sur Linux, seules les cibles
> Android sont compilées ; iOS/Mac Catalyst sont ajoutés hors Linux, et Windows uniquement
> sur Windows. C'est le comportement par défaut du template MAUI et il n'est pas nécessaire de le modifier.

---

## Prérequis

### Commun à tous les systèmes

- **SDK .NET 10.0.4xx** — vérifier avec `dotnet --version`
- **Workload MAUI** correspondant à votre plateforme :

```bash
# Android (Linux, macOS, Windows)
dotnet workload install maui-android

# Windows
dotnet workload install maui-windows

# macOS (iOS + Mac Catalyst)
dotnet workload install maui-ios maui-maccatalyst
```

Vérification :

```bash
dotnet workload list
```

### Linux

- **JDK 17** (`java -version` doit afficher 17)
- **Android SDK** via `commandlinetools` (platform-tools, platform 35+, build-tools, emulator)
- Variable `ANDROID_HOME` pointant vers le SDK, ou installation à l'emplacement standard `~/Android/Sdk`
- Un **appareil Android** (USB avec débogage activé) ou un **émulateur** (AVD)

### Windows

- Windows 10 version 1809 (17763) minimum
- **Visual Studio 2022** (17.12+) avec la charge *Développement mobile et multiplateforme .NET* — recommandé, car il fournit le SDK Windows et l'interface de déploiement
- SDK Android (optionnel, pour cibler Android depuis Windows)

### macOS

- macOS 13+ pour Mac Catalyst, iOS 15+ pour le simulateur iOS
- **Xcode 16+** avec la ligne de commande `xcode-select --install` (obligatoire pour toute cible iOS/Mac Catalyst)
- CocoaPods (`sudo gem install cocoapods`) pour le déploiement sur appareil iOS

---

## Démarrage rapide

```bash
# 1. Cloner le dépôt
git clone git@github.com:TSEFACKDEV/calculatrice_mobile_klein_Dev.git
cd calculatrice_mobile_klein_Dev

# 2. Installer les dépendances
dotnet restore

# 3. Compiler pour vérifier que tout est correct
dotnet build

# 4. Lancer (cible Android par défaut sur Linux)
dotnet build -t:Run -f net10.0-android
```

Sur Windows, remplacez l'étape 4 par :

```bash
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

> À la première compilation, le SDK Android télécharge automatiquement les
> composants manquants. Les compilations suivantes prennent de quelques secondes à une dizaine.

---

## Exécuter sur chaque OS

### Linux (Android uniquement)

Linux ne peut pas compiler les cibles iOS/Mac Catalyst/Windows, mais Android fonctionne nativement.

```bash
# Vérifier que le_sdk Android est bien détecté
echo $ANDROID_HOME

# Lancer sur l'émulateur
emulator -avd MauiApi36 &          # adaptez le nom de votre AVD
adb wait-for-device
dotnet build -t:Run -f net10.0-android
```

Pour un appareil physique : branchez-le en USB, activez le **débogage USB**, puis :

```bash
adb devices      # l'appareil doit apparaître comme « device »
dotnet build -t:Run -f net10.0-android
```

### Windows (Windows + Android)

```powershell
# Cible Windows (application desktop non packagée)
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# Cible Android, depuis le même PC
dotnet build -t:Run -f net10.0-android
```

L'IDE Visual Studio 2022 détecte automatiquement le profil *Windows Machine* défini dans
`Properties/launchSettings.json` (F5 pour lancer).

### macOS (Mac Catalyst, simulateur iOS, Android)

```bash
# Mac Catalyst (application native macOS)
dotnet build -t:Run -f net10.0-maccatalyst

# Simulateur iOS (choisir l'architecture de votre Mac)
dotnet build -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64   # Apple Silicon
dotnet build -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-x64    # Intel

# Android
dotnet build -t:Run -f net10.0-android
```

Vérifiez que Xcode est bien configuré avant toute cible iOS :

```bash
xcode-select -p && xcodebuild -version
```

### Tableau récapitulatif

| OS hôte | Android | iOS / simulateur | Mac Catalyst | Windows |
|---|---|---|---|---|
| **Linux** | Oui | Non | Non | Non |
| **Windows** | Oui | Non | Non | Oui |
| **macOS** | Oui | Oui | Oui | Non |

---

## Tester l'application

### 1. Tests de compilation (non-régression)

```bash
# Build complet sur les cibles disponibles
dotnet build

# Build explicite par cible
dotnet build -f net10.0-android
dotnet build -f net10.0-windows10.0.19041.0   # Windows uniquement
dotnet build -f net10.0-maccatalyst            # macOS uniquement

# Nettoyage complet si un build est corrompu
dotnet clean && rm -rf bin obj && dotnet restore && dotnet build
```

### 2. Plan de test manuel

Le projet ne contient pas de tests automatisés ; le protocole de validation repose sur
les scénarios fonctionnels suivants, à exécuter après chaque modification.

#### Standard

| # | Action | Résultat attendu |
|---|---|---|
| S1 | `12 + 7 =` | `19` |
| S2 | `2 + 3 × 4 =` | `14` (priorité de `×`) |
| S3 | `(2 + 3) × 4 =` | `20` (parenthèses) |
| S4 | `10 + 5 ×` puis `2 =` | `20`, parenthèses fermées automatiquement |
| S5 | `50 %` | `0.5` |
| S6 | `9 √` | `3` |
| S7 | `5 x!` | `120` |
| S8 | Touche `⌫` (retour arrière) après `123` | `12` |
| S9 | `1 ÷ 0 =` | Message d'erreur, pas de crash |

#### Scientifique

| # | Action | Résultat attendu |
|---|---|---|
| C1 | `sin(30)` en DEG | `0.5` |
| C2 | Même calcul après bascule en RAD | Valeur en radians |
| C3 | `log(1000)` | `3` |
| C4 | `π` puis `=` | `3.14159265…` |
| C5 | `2 + 3 =` puis `Ans × 2 =` | `10` |

#### Programmeur

| # | Action | Résultat attendu |
|---|---|---|
| P1 | Basculer sur HEX, saisir `FF` | `255` |
| P2 | Basculer sur BIN | `11111111` |
| P3 | `12 AND 10` en DEC | `8` |
| P4 | `1 << 4` | `16` |
| P5 | `NOT 0` | `-1` |

#### Convertisseur

| # | Action | Résultat attendu |
|---|---|---|
| U1 | `1 km` vers `m` | `1000 m` |
| U2 | `100 °C` vers `°F` | `212 °F` |
| U3 | `1 kg` vers `lb` | `2.20462262 lb` |
| U4 | Bouton d'échange des unités | Les valeurs source/cible sont inversées |
| U5 | Saisie d'un texte non numérique | Message « Valeur invalide » |

#### Commun

| # | Action | Résultat attendu |
|---|---|---|
| M1 | Effectuer 5 calculs, ouvrir l'historique | 5 entrées, horodatées, dans l'ordre inverse |
| M2 | Taper sur une entrée d'historique | L'expression est rechargée dans la saisie |
| M3 | `MS` puis `MR` | La valeur mémorisée est réinsérée |
| M4 | Effectuer 65 calculs | L'historique est plafonné à 60 entrées |
| M5 | Changer de mode 4 fois puis revenir | L'état d'affichage est cohérent, aucun crash |

### 3. Test du moteur de calcul isolément

Le moteur (`Services/CalculatorEngine.cs`) ne dépend d'aucune API MAUI : il peut être validé
sans appareil en créant un projet de test à côté et en y référençant le projet :

```bash
dotnet new xunit -n Calculatrice.Tests
dotnet add Calculatrice.Tests/Calculatrice.Tests.csproj reference Calculatrice.csproj
```

Puis écrire des tests sur `MathEngine.Evaluate("...")` et `UnitConverter.Convert(...)`.

---

## Structure du projet

```
Calculatrice/
├── Calculatrice.csproj          # Définition des cibles (Android, iOS, Mac Catalyst, Windows)
├── MauiProgram.cs               # Amorçage de l'application MAUI
├── App.xaml / App.xaml.cs       # Application et ressources globales
├── AppShell.xaml(.cs)           # Coquille de navigation
├── MainPage.xaml                # Interface complète (thème sombre, pads, panneaux)
├── MainPage.xaml.cs             # Logique UI : saisie clavier, modes, mémoire, historique
├── Services/
│   └── CalculatorEngine.cs      # Moteur : parseur, fonctions, bases, convertisseur d'unités
├── Platforms/                   # Code spécifique par OS (Android, iOS, Mac Catalyst, Windows)
├── Resources/                   # Icônes, splash screen, polices (Open Sans), styles
└── Properties/
    └── launchSettings.json      # Profils de lancement (Windows)
```

### Points d'entrée à connaître

| Besoin | Emplacement |
|---|---|
| Ajouter une fonction mathématique | `Services/CalculatorEngine.cs` (dispatch des fonctions, ~ligne 393) |
| Ajouter une catégorie d'unités | `Services/CalculatorEngine.cs` (tableau `Categories`, ~ligne 588) |
| Ajouter une touche du pave | `MainPage.xaml` (pad correspondant) **et** `OnKeyClicked` dans `MainPage.xaml.cs:492` |
| Modifier l'apparence | `MainPage.xaml`, section `ContentPage.Resources` (palette et styles) |
| Modifier le comportement mémoire / historique | `MainPage.xaml.cs:664` et `MainPage.xaml.cs:698` |

---

## Dépannage

| Symptôme | Cause probable | Solution |
|---|---|---|
| `workload maui-android is not installed` | Workload absent | `dotnet workload install maui-android` |
| `Could not find android sdk` | `ANDROID_HOME` non défini | `export ANDROID_HOME=$HOME/Android/Sdk` |
| `Java SDK 17 not found` | JDK absent ou trop ancien | Installer OpenJDK 17, puis `export JAVA_HOME=<chemin>` |
| `No buildable projects` sur Linux | Tentative de build iOS/Mac Catalyst | Utiliser `-f net10.0-android` |
| `The application could not be started` | Aucun appareil/émulateur | `adb devices`, démarrer l'AVD, vérifier le câble USB |
| Erreurs XAML après modification du thème | Cache XAML obsolète | `dotnet clean` puis rebuild ; supprimer `obj/` si le problème persiste |
| Erreur de signature Apple | Certificat de développement absent | Ouvrir le projet dans Xcode et sélectionner une équipe de développement |

---

## Licence

Projet à vocation pédagogique (cours GLO5). Tous droits réservés — l'utilisation, la
modification et la redistribution du code sont libres dans un cadre non commercial.
