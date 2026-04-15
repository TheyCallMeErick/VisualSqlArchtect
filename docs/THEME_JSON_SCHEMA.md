# Theme JSON Schema (Fase 5)

Arquivo padrao: `src/AkkornStudio.UI/Assets/Themes/user-theme.json`

Objetivo:
- Permitir customizacao de tokens macro sem editar AXAML.
- Manter fallback seguro para valores invalidos.

## Estrutura

```json
{
  "meta": {
    "name": "Studio Dark",
    "version": "1.0"
  },
  "colors": {
    "bg0": "#090B14",
    "bg1": "#0F1220",
    "bg2": "#151A2C",
    "bg3": "#1C2338",
    "bg4": "#252F49",
    "textPrimary": "#E7ECFF",
    "textSecondary": "#AEB9D9",
    "textMuted": "#7F8AAE",
    "textDisabled": "#66708F",
    "textInverse": "#0B0F1D",
    "textAccent": "#8FA7FF",
    "btnPrimaryBg": "#2563EB",
    "btnPrimaryFg": "#F8FAFC",
    "btnWarningBg": "#7C2D12",
    "btnWarningFg": "#FED7AA"
  },
  "typography": {
    "uiFont": "Manrope,Sora,Segoe UI,Arial,sans-serif",
    "nodeFont": "Space Grotesk,Manrope,Segoe UI,Arial,sans-serif",
    "monoFont": "JetBrainsMono Nerd Font,JetBrains Mono,Cascadia Code,Consolas,monospace",
    "displaySize": 24,
    "headingSize": 18,
    "titleSize": 15,
    "nodeTitleSize": 14,
    "labelSize": 13,
    "bodySize": 12,
    "captionSize": 11,
    "monoBodySize": 12,
    "monoSmallSize": 11
  }
}
```

## Mapeamento de chaves

- `colors.bg0` -> `Bg0`, `Bg0Brush`
- `colors.bg1` -> `Bg1`, `Bg1Brush`
- `colors.bg2` -> `Bg2`, `Bg2Brush`
- `colors.bg3` -> `Bg3`, `Bg3Brush`
- `colors.bg4` -> `Bg4`, `Bg4Brush`
- `colors.textPrimary` -> `TextPrimary`, `TextPrimaryBrush`
- `colors.textSecondary` -> `TextSecondary`, `TextSecondaryBrush`
- `colors.textMuted` -> `TextMuted`, `TextMutedBrush`
- `colors.textDisabled` -> `TextDisabled`, `TextDisabledBrush`
- `colors.textInverse` -> `TextInverse`, `TextInverseBrush`
- `colors.textAccent` -> `TextAccent`, `TextAccentBrush`
- `colors.btnPrimaryBg` -> `BtnPrimaryBg`, `BtnPrimaryBgBrush`
- `colors.btnPrimaryFg` -> `BtnPrimaryFg`, `BtnPrimaryFgBrush`
- `colors.btnWarningBg` -> `BtnWarningBg`, `BtnWarningBgBrush`
- `colors.btnWarningFg` -> `BtnWarningFg`, `BtnWarningFgBrush`
- `typography.uiFont` -> `UIFont`
- `typography.nodeFont` -> `NodeFont`
- `typography.monoFont` -> `MonoFont`
- `typography.displaySize` -> `FontSizeDisplay`
- `typography.headingSize` -> `FontSizeHeading`
- `typography.titleSize` -> `FontSizeTitle`
- `typography.nodeTitleSize` -> `FontSizeNodeTitle`
- `typography.labelSize` -> `FontSizeLabel`
- `typography.bodySize` -> `FontSizeBody`
- `typography.captionSize` -> `FontSizeCaption`
- `typography.monoBodySize` -> `FontSizeMonoBody`
- `typography.monoSmallSize` -> `FontSizeMonoSmall`

## Regras de seguranca

- Arquivo ausente: aplica tema padrao (sem erro).
- JSON invalido: fallback para tema padrao.
- Chave invalida (cor/tamanho): ignorada individualmente com warning.
- Tokens sem valor valido permanecem no padrao.
- Escopo atual: tokens de shell, tipografia global e hierarchy de texto.
