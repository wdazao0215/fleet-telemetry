---
name: feature-branch-flow
description: >
  Runs the git flow for this repo — create the feature branch, make Conventional Commits in English,
  open a PR with the project template and merge to main. Use when starting a new feature, when the
  user says "abre la rama", "haz el PR", "mergea a main", or when a feature is finished and needs to
  land.
---

# Flujo de rama por feature

El enunciado de la prueba evalúa explícitamente el historial: quieren ver el proceso de pensamiento,
no un "Initial commit". Cada feature es una rama, un PR y un merge.

## Iniciar

```bash
git switch main && git pull --ff-only
git switch -c feat/<slug>
```

Prefijos: `feat/`, `fix/`, `refactor/`, `docs/`, `chore/`.

## Commits

Conventional Commits, en inglés, en imperativo. El cuerpo explica **por qué**, no qué archivo cambió.

```
feat(ingestion): deduplicate GPS positions with a Redis NX window

The simulator injects 10% duplicates by design, and the stopped-vehicle rule
compares consecutive positions — counting a duplicate as movement would mask a
real stop. A SET NX with a 10s TTL keeps the check O(1) and survives restarts.
```

Commits pequeños y coherentes. Si el mensaje necesita "y" para describir lo que hace, son dos commits.

## Pull Request

```bash
git push -u origin feat/<slug>
gh pr create --title "feat(<scope>): <resumen>" --body "$(cat <<'BODY'
## Qué

## Por qué

## Cómo verificarlo
```bash
docker compose up --build
```

## Checklist
- [ ] Tests de las reglas críticas
- [ ] Regla de capas respetada (`Architecture.Tests` en verde)
- [ ] Verificado ejecutándolo, no solo compilando
BODY
)"
```

## Merge

```bash
gh pr merge --merge --delete-branch    # merge commit: preserva la historia de la feature
git switch main && git pull --ff-only
```

Se usa merge commit y no squash: el enunciado quiere ver el proceso, y el squash lo borra.

## Reglas

- **Nunca commits directos a `main`.**
- Un PR no se mergea con el CI en rojo.
- El PR describe cómo verificar la feature, no solo qué cambió.
