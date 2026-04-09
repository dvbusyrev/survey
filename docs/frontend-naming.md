# Frontend Naming Guide

This document defines naming conventions for frontend markup and scripts.

## Goals

- keep markup discoverable and predictable;
- avoid coupling JS to fragile DOM ids;
- make CSS reusable across pages.

## Markup Rules

- Use `data-role` for JS hooks.
- Use semantic BEM-like classes for styling:
  - page block: `survey-editor-page`
  - element: `survey-editor-page__criteria-list`
  - modifier: `survey-editor-page__criteria-list--compact`
- Use `id` only for:
  - form semantics (`label for="..."`);
  - browser-native anchors;
  - external integrations that strictly require `id`.

## JS Selector Rules

- Prefer `document.querySelector('[data-role="..."]')` for behavior.
- Do not add new logic that depends on historical ids like:
  - `two_step`
  - `cont_criteries`
  - `add_crit`
  - `conf_btn`
  - `add_survey_btn`
  - `block_btn_csp`
  - `block_btn_not_csp`
- When refactoring old code, migrate selectors from `id` to `data-role`.

## Modal Rules

- Keep modal structure consistent:
  - `modal-header`
  - `modal-body`
  - `modal-footer`
- Close button must be:
  - inside `modal-header`
  - `<button type="button" class="modal-close">...</button>`

## Button Rules

- Every non-submit button must explicitly use `type="button"`.
- Avoid implicit default submit behavior.

## Suggested `data-role` Vocabulary

- `criteria-step`
- `criteria-list`
- `criteria-add`
- `criteria-confirm`
- `survey-submit`
- `selected-organizations-container`
- `selected-organizations-list`
- `send-email`
- `sign-actions`
- `signed-actions`
- `csp-download-actions`
- `main-table`
