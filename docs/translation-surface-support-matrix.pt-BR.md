<!--
  Copyright (c) lokinmodar. All rights reserved.
  Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
-->

# Matriz de suporte das superfícies de tradução

Este documento é o inventário canônico das superfícies de tradução configuráveis pelo usuário no Echoglossian.

Ele deve ser atualizado sempre que uma nova superfície, modo ou restrição de release for adicionada ou removida.

## Fluxo de ativação

```mermaid
flowchart TD
    A[Abrir a configuração do plugin] --> B[Escolher o idioma de destino]
    B --> C[Escolher a engine de tradução]
    C --> D{A engine está configurada?}
    D -- Não --> E[A tradução permanece desativada]
    E --> E1[Mostrar notificação persistente do Dalamud]
    E1 --> E2[Abrir a configuração e corrigir a engine]
    D -- Sim --> F{O idioma exige arquivos de fonte baixados?}
    F -- Sim, arquivos ausentes --> G[A tradução permanece desativada]
    G --> G1[Mostrar orientação sobre os assets e opção de rechecagem]
    F -- Não ou arquivos presentes --> H[Ativar a tradução global]
    H --> I[Escolher quais superfícies traduzir]
    I --> J[Diálogos e overlays]
    I --> K[Superfícies de quest e journal]
    I --> L[Toasts]
    I --> M[Janelas do jogo]
    I --> N[Família opcional de tooltips quando for reativada]
```

## Famílias de modos de tradução

| Família de modos | Modos | Utilizada por |
| --- | --- | --- |
| Família quest / native-window | `Native UI Translation`, `Tooltip Translation Only`, `Native UI Translation With Original Tooltips` | Superfícies da família Journal e janelas do jogo DB-first |
| Família overlay | `Native UI Translation`, `Overlay Translation Only`, `Native UI Translation With Original Overlay` | Talk, BattleTalk, legendas, MiniTalk, CutSceneSelectString e a família de toasts |

## Superfícies de diálogo e overlay

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Talk | `TranslateTalk` | Família overlay | Suporta nomes de NPC traduzidos por `TranslateTalkNpcNames` | Ativado |
| BattleTalk | `TranslateBattleTalk` | Família overlay | Suporta nomes de NPC traduzidos por `TranslateBattleTalkNpcNames` | Ativado |
| TalkSubtitle | `TranslateTalkSubtitle` | Família overlay | Apresentação em overlay sem barra de título quando o modo overlay está ativo | Ativado |
| MiniTalk | `TranslateMiniTalk` | Família overlay | Superfície nativa pequena; textos mais verbosos ainda exigem native reflow cuidadoso | Ativado |
| CutSceneSelectString | `TranslateCutSceneSelectString` | Família overlay | A pergunta vira o título e as opções viram o corpo no modo overlay | Ativado |

## Superfícies de quest e journal

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Journal | `TranslateJournal` | Família quest / native-window | Superfície de lista de quests | Ativado |
| JournalDetail | `TranslateJournalDetail` | Família quest / native-window | Layout de corpo denso; o modo nativo exige block reflow explícito | Ativado |
| ToDoList | `TranslateToDoList` | Família quest / native-window | Rastreador de quest / lista de objetivos | Ativado |
| ScenarioTree | `TranslateScenarioTree` | Família quest / native-window | Rastreador do cenário principal | Ativado |
| JournalAccept | `TranslateJournalAccept` | Família quest / native-window | Janela de aceite de quest | Ativado |
| JournalResult | `TranslateJournalResult` | Família quest / native-window | Janela de resultado / conclusão de quest | Ativado |
| RecommendList | `TranslateRecommendList` | Família quest / native-window | Lista de recomendações | Ativado |
| AreaMap | `TranslateAreaMap` | Família quest / native-window | Texto de quest dentro da UI de quests relacionada ao mapa | Ativado |

## Superfícies de toast

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| WideText / Screen Info toast | `TranslateWideTextToast` | Família overlay | Toast informativo grande no centro da tela | Ativado |
| Error toast | `TranslateErrorToast` | Família overlay | Notificações de erro ou falha | Ativado |
| Area toast | `TranslateAreaToast` | Família overlay | Notificações de área e localização | Ativado |
| Class / Job change toast | `TranslateClassChangeToast` | Família overlay | Anúncio de troca de class/job | Ativado |
| Text gimmick hint | `TranslateTextGimmickHint` | Família overlay | Superfície de dica de gimmick/tutorial | Ativado |
| Quest toast | `TranslateQuestToast` | Família overlay | Toast de notificação relacionado a quest | Ativado |

## Superfícies de janelas do jogo

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Character window | `TranslateCharacterWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Main Command | `TranslateMainCommandWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Action Menu | `TranslateActionMenuWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| HUD windows | `TranslateHudWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Operation Guide | `TranslateOperationGuideWindow` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |
| Addon Context Menu Title | `TranslateAddonContextMenuTitle` | Família quest / native-window | Runtime DB-first de janelas do jogo | Ativado |

## Superfícies ocultas ou temporariamente restritas

| Superfície | Toggle de configuração | Modos | Notas | Status da release atual |
| --- | --- | --- | --- | --- |
| Action / item detail tooltips | `TranslateTooltips` | Família overlay | A tradução estruturada de tooltips é desativada à força na inicialização enquanto `ActionDetail` / `ItemDetail` continuarem instáveis | Temporariamente desativado para a release |
| Yes/No dialog | `TranslateYesNoScreen` | Apenas toggle | Presente no modelo de configuração e na implementação da aba, mas não está exposto atualmente no fluxo ativo da aba Overlay | Implementado, mas oculto na UI atual |
| SelectString dialog | `TranslateSelectString` | Apenas toggle | Presente no modelo de configuração e na implementação da aba, mas não está exposto atualmente no fluxo ativo da aba Overlay | Implementado, mas oculto na UI atual |
| SelectOk dialog | `TranslateSelectOk` | Apenas toggle | Presente no modelo de configuração e na implementação da aba, mas não está exposto atualmente no fluxo ativo da aba Overlay | Implementado, mas oculto na UI atual |

## Notas operacionais

| Tópico | Comportamento |
| --- | --- |
| Ativação global | A tradução não permanece ativada a menos que a engine selecionada seja válida e esteja configurada para o idioma escolhido |
| Arquivos de fonte baixados | Alguns idiomas exigem arquivos de fonte baixados antes que a tradução possa ser ativada com segurança |
| Idiomas somente overlay | Quando o idioma é overlay-only, os modos de substituição nativa são normalizados para apresentação em overlay/tooltip |
| Ativação por superfície | Cada família continua exigindo seu próprio toggle por superfície mesmo depois de a tradução global ser ativada |
| Gating de release | Uma superfície pode existir na configuração ou no código e ainda assim estar propositalmente oculta ou forçadamente desativada em uma determinada release |

## Regras de manutenção

- Atualize esta matriz sempre que uma nova superfície de tradução for adicionada.
- Atualize esta matriz sempre que uma superfície mudar de família de modos.
- Atualize esta matriz sempre que uma release desativar ou ocultar temporariamente uma funcionalidade.
- Deve-se priorizar documentar o comportamento real em runtime, e não um comportamento apenas aspiracional.
