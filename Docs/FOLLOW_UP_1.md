# 📄 Rapport de Suivi #1 — Virteem (Virtual Tour)

**Projet :** Journey Through XR (Epitech x RandomCorp™)  
**Sujet Choisi :** Sujet #2 — Virteem: Virtual Tour & Virtual Open House  

---

## 1. Justification du Choix du Sujet & Stratégie

Nous avons sélectionné le **Sujet #2 (Virteem)** car il répond à un enjeu concret d'orientation et de découverte d'infrastructures à distance sans contrainte géographique. 

L'objectif est d'offrir une visite virtuelle immersive, engageante et autonome d'un établissement. Ce projet repose sur trois piliers clés imposés par le cahier des charges :
* **Un Ancrage Réel (*Building Scan*) :** Capturer un espace physique réel et l'intégrer comme environnement 3D navigable.
* **Des Interactions Réparties (3 Zones) :** Intégrer au moins 3 espaces distincts contenant des éléments déclencheurs (panneaux, vidéos, animations).
* **Une Accessibilité 100% Autonome :** Garantir un système d'Onboarding et une ergonomie permettant une exploration sans guide ni instructeur extérieur.

---

## 2. Organisation de l'Équipe & Pipeline Cross-Platform

Notre groupe est composé de 3 membres, avec une répartition équilibrée selon les compétences et le matériel de chacun :

* **Lead Dev & Chef de Projet (Laud) :**
  * Rédaction du Storyboard, cadrage du parcours utilisateur et rédaction de la documentation (`Docs/`).
  * Architecture logicielle du projet Unity, structure du dépôt Git et versionnement.
  * Développements C# : moteurs d'interaction, gestion des déclencheurs (*triggers*) et événements dans les 3 zones.
  * Génération et déploiement des versions de test Android (`.apk`).

* **UX/UI & Pipeline iOS (Floriaan) :**
  * Conception visuelle des interfaces (UI/Canvas) et du système d'Onboarding autonome (tutoriel d'accueil).
  * Configuration Xcode, signatures et validation des compilations iOS (iPhone).
  * Réalisation et montage de la vidéo promotionnelle finale.

* **3D & Scan Specialist (Timothy) :**
  * Scan 3D du bâtiment réel, traitement des nuages de points et nettoyage des maillages.
  * Modélisation de l'environnement virtuel et des 3 zones sous forme de primitives Unity[cite: 1].
  * Habillage spatial, gestion de l'éclairage et placement des déclencheurs interactifs[cite: 1].

---

## 3. Planning Détaillé des Jalons

[Semaine 1 : Bootstrap & Cadrage]
├── Validation du Bootstrap AR (Camera Vuforia + Rendu Cube 3D)[cite: 1]
├── Rédaction de la documentation & Organisation du dépôt GitHub[cite: 1]
└── Validation du Storyboard et repérage du bâtiment à scanner[cite: 1]
│
[Semaine 2 : Capture 3D & Structuration des Zones]
├── Réalisation du Scan 3D du bâtiment physique[cite: 1]
├── Modélisation des 3 zones en primitives dans Unity[cite: 1]
└── Intégration de l'interface d'Onboarding autonome (Tutoriel)[cite: 1]
│
[Semaine 3 : Interactions, Assets & Builds Multiplateformes]
├── Scripting C# des éléments interactifs (Panneaux, Médias, Animations)[cite: 1]
├── Intégration des assets fournis par la division Media de RandomCorp™[cite: 1]
└── Recette croisée et validation des builds Android (.apk) et iOS (Xcode)[cite: 1]
│
[Semaine 4 : Recette Finale & Soutenance]
├── Tests d'utilisabilité en mode 100% autonome (sans aide extérieure)[cite: 1]
├── Tournage & Montage de la vidéo promotionnelle centrée sur l'expérience[cite: 1]
└── Présentation finale du Proof of Concept (POC) devant la direction[cite: 1]

---

## 4. Storyboard du Parcours Utilisateur

1. **Phase 1 : Onboarding & Démarrage Autonome**
   * L'utilisateur lance l'application sur son smartphone (Android ou iOS).
   * Un panneau UI d'accueil apparaît pour expliquer le fonctionnement : *"Pointez votre caméra vers la cible au sol/mural pour charger la visite virtuelle"*.

2. **Phase 2 : Ancrage & Entrée dans le Bâtiment Virtualisé**
   * Une fois la cible détectée par Vuforia, le modèle 3D du bâtiment scanné (construit en primitives) apparaît superposé au monde réel[cite: 1].
   * Un premier repère visuel (mini-carte ou flèches au sol) indique les zones explorables[cite: 1].

3. **Phase 3 : Exploration des 3 Zones Interactives**
   * **Zone 1 (Hall d'Accueil) :** L'utilisateur s'approche d'un totem interactif. Un clic déclenche l'ouverture d'un panneau d'information présentant l'établissement[cite: 1].
   * **Zone 2 (Espace Pédagogique / Salle de Cours) :** L'utilisateur interagit avec une primitive dédiée, déclenchant la lecture d'une vidéo de démonstration ou d'une animation[cite: 1].
   * **Zone 3 (Espace Vie Étudiante & Loisirs) :** Un élément interactif dévoile des détails sur la vie de campus et les infrastructures[cite: 1].

4. **Phase 4 : Clôture de la Visite**
   * L'utilisateur peut réinitialiser la vue ou quitter l'expérience à tout moment via un menu d'interface persistant.

---

## 5. État du Bootstrap & Démo Technique

* **Statut de la chaîne technique :**
  * Licence Vuforia configurée et validée[cite: 1].
  * Suivi de caméra (*AR Camera*) et détection d'ImageTarget opérationnels[cite: 1].
  * Rendu 3D validé avec l'affichage d'une primitive (Cube 3D)[cite: 1].
  * Génération des exécutables de test validée sur Android (`.apk`) et iOS (Xcode)[cite: 1].