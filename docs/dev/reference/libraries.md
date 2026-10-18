---
title: Внутрішні бібліотеки
description: ProjectK.Optimization (закритий пакет планування) і ProjectK.3PDB.Standalone (база третьопробників).
sidebar:
  order: 3
---

## ProjectK.Optimization

Закритий внутрішній NuGet-пакет, на якому працює [планування таборів](/user/features/planning/):
приймає період пошуку, тривалість і зайнятість учасників з вагами, повертає дату з найменшими
конфліктами. Публікується в GitHub Packages, підключається через `NUGET_AUTH_TOKEN`
(див. [Як запустити проєкт](/dev/guides/getting-started/)). Внутрішня будова і алгоритм не
документуються публічно.

## ProjectK.3PDB.Standalone

Окремий застосунок «база третьопробників» ([код](https://github.com/lileyka-app/ProjectK.3PDB.Standalone)).

- **Імпорт CSV.** Дані зводяться в таблицю (найпростіше в Google Таблицях) за
  [шаблоном](https://docs.google.com/spreadsheets/d/1mEPSekOaTu657UiXf6jgqu3bRScOASFC1z1i1cUIeIA/edit?usp=sharing);
  назви колонок мають збігатися з шаблоном. Далі файл експортується в CSV і завантажується в
  застосунку через «Імпорт».
- **Збірка й публікація.** Реліз на GitHub — пайплайн збирає все сам.
