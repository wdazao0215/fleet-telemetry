---
name: adr-writer
description: >
  Writes an Architecture Decision Record in docs/adr using the MADR format, in Spanish. Use when a
  technical decision needs to be recorded or revisited — "documenta la decisión de X", "escribe el
  ADR", "por qué elegimos Y", or after choosing between two technologies.
---

# Escribir un ADR

Los ADRs viven en `docs/adr/NNNN-<slug>.md`, en español, formato MADR. El criterio de evaluación de
la prueba dice literalmente: "Por qué elegiste X base de datos o Y framework de frontend". Esto es lo
que responde a eso.

## Numeración

Cuatro dígitos, secuencial: `0001-`, `0002-`. Nunca se reescribe un ADR aceptado; si la decisión
cambia, se escribe uno nuevo con estado `Sustituye a NNNN` y el viejo pasa a `Sustituido por MMMM`.

## Plantilla

```markdown
# NNNN — <Título de la decisión>

- **Estado:** Aceptada | Propuesta | Sustituida por NNNN
- **Fecha:** AAAA-MM-DD

## Contexto

Qué problema nos obliga a decidir. Restricciones reales: plazo, stack existente del cliente,
lo que el enunciado exige.

## Decisión

Qué elegimos, en una frase afirmativa. "Usamos X para Y."

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|

Esta tabla es la parte que más se lee. Una alternativa descartada sin razón explícita es una decisión
sin fundamentar.

## Consecuencias

**Positivas:** qué habilita.
**Negativas:** qué nos cuesta — y esto se escribe de verdad, no se maquilla.
**Reversibilidad:** qué tan caro es cambiar de opinión más adelante.
```

## Criterio

- Un ADR sin consecuencias negativas está incompleto: toda decisión cuesta algo.
- Documenta lo que fue una **elección**, no lo obvio. "Usamos Git" no es un ADR.
- Si tuviste que descartar algo por tiempo y no por técnica, dilo así.

## Al terminar

Añade la entrada al índice de `docs/adr/README.md` y enlázala desde el README raíz si la decisión es
de las que un evaluador va a buscar.
