<!-- Supplementary documentation for DaisyPasswordBox -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyPasswordBox is a password field in the style of [DaisyInput](DaisyInput.md), with the same **10 variants** and **5 sizes**, a label, hint and helper texts, start and end icons, and an eye button that shows or hides the password. It is a `TemplatedControl` around an inner `TextBox` (`PART_TextBox`) that masks the text, not a `TextBox` itself: the text is `Password`, not `Text`, and the placeholder is `Watermark`.

## Password

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Password` | `string` | `""` | The password. Binds two-way by default. |
| `MaxLength` | `int` | `0` | Maximum number of characters; 0 means no limit. |
| `PasswordChar` | `char` | `●` | Character that masks the password. |
| `IsPasswordRevealButtonEnabled` | `bool` | `true` | Shows the eye button that reveals the password. |
| `IsPasswordVisible` | `bool` (read-only) | `false` | True while the eye button shows the password. The eye opens and the control has the `:password-visible` pseudo-class. Only the eye button changes it. |
| `HasText` | `bool` (read-only) | `false` | True while `Password` is not empty; the control then has the `:hastext` pseudo-class. |

`PasswordChanged` (`EventHandler<RoutedEventArgs>`) is raised after every change of `Password`, whether it was typed or set from code or a binding. It is a plain .NET event, not a routed event; XAML attaches a handler as usual (`PasswordChanged="OnPasswordChanged"`). `SelectAll()` selects the whole password in the field.

## Look

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Variant` | `DaisyInputVariant` | `Bordered` | Field style (see below). |
| `Size` | `DaisySize` | `Medium` | Height, padding, font size, corner radius and icon size (see below). Without an explicit value it follows the global size of [FlowerySizeManager](FlowerySizeManager.md). |
| `StartIcon` | `StreamGeometry?` | `null` | Icon before the text, in the base text color. |
| `EndIcon` | `StreamGeometry?` | `null` | Icon at the end, after the eye button. |
| `BorderRingBrush` | `IBrush?` | `null` | Declared like on `DaisyInput`, but the current template does not use it: the focus border takes the color described below. |

`Background`, `Foreground`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding`, `FontSize` and `MinHeight` reach the field as on any templated control; the variant and size styles set them.

`DaisyInputVariant`, `DaisyLabelPosition` and `DaisySize` are the types in `Flowery.Controls`. `Flowery.Enums` declares types of the same names, so a C# file that imports both namespaces writes `Flowery.Controls.DaisyInputVariant`.

### Variants

| Variant | Look |
| --- | --- |
| Bordered (default) | `DaisyBase100Brush` field with a 1 px border in the base text color. |
| Ghost | Transparent, no border; the field fills with `DaisyBase200Brush` on focus. |
| Filled | `DaisyBase200Brush` field with a 2 px bottom border and rounded top corners. |
| Primary / Secondary / Accent | Border in the color. |
| Info / Success / Warning / Error | Border in the semantic color. |

On focus, the border grows to 2 px, in the Primary color for Bordered, Ghost and Filled and in the variant color for the colored variants.

### Sizes

The heights are minimum heights from the shared `DaisySize*Height` tokens; with `LabelPosition="Floating"` the `DaisyInputFloating*Height` tokens apply, which leave room for the floated label.

| Size | Height | Floating height | Font size | Icon size |
| --- | --- | --- | --- | --- |
| ExtraSmall | 24 | 36 | 10 | 10 |
| Small | 28 | 40 | 12 | 12 |
| Medium (default) | 32 | 44 | 14 | 16 |
| Large | 36 | 48 | 18 | 20 |
| ExtraLarge | 40 | 50 | 20 | 24 |

## Label and Texts

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Label` | `string?` | `null` | Name of the field, semibold. |
| `LabelPosition` | `DaisyLabelPosition` | `Top` | `Top` puts the label above the field; `Floating` puts it in the field and floats it onto the top border (see below). `None` and `Inset` have no layout of their own and look like `Top`; leave `Label` empty for a field without a label. |
| `IsRequired` | `bool` | `false` | Red asterisk after the label; also marks the field as required for UI Automation. |
| `IsOptional` | `bool` | `false` | The muted text "Optional" after the label. The text is English in every language. |
| `HintText` | `string?` | `null` | Muted text above the field, below the label. |
| `HelperText` | `string?` | `null` | Muted text below the field, right-aligned, such as the password rules. |
| `Watermark` | `string?` | `null` | Placeholder while the field is empty. |

### Floating Label

With `LabelPosition="Floating"` the label sits in the field at half opacity while the field has neither focus nor a password. On focus, or once it has a password, the label shrinks to 85 % (80 % for ExtraSmall), moves onto the top border in a small `DaisyBase100Brush` chip with a Primary border, and turns Primary and semibold. The asterisk and "Optional" move with it. The watermark shows only while the field has focus and is still empty, so the label and the watermark never overlap. The hint text stays above the field.

## Scaling

DaisyPasswordBox implements `IScalableControl`. Inside a container with `FloweryScaleManager.EnableScaling="True"`, [FloweryScaleManager](FloweryScaleManager.md) sets two read-only font sizes:

| Property | Base | Minimum | Used for |
| --- | --- | --- | --- |
| `ScaledLabelFontSize` | 12 | 10 | Label row, asterisk, "Optional", hint and helper texts. |
| `ScaledTextFontSize` | 14 | 11 | Not used by the current template; the password text follows `FontSize`, which `Size` sets. Available to custom templates. |

Each is the base times the scale factor, but at least the minimum.

## Accessibility

The inner field carries the UI Automation name: `AutomationProperties.Name` of the control, else `Label`, else `Watermark`, else the localized "Password". Its help text is `AutomationProperties.HelpText`, else `HelperText`, else `HintText`. `IsRequired` or `AutomationProperties.IsRequiredForForm` marks it as required. The eye button is named "Show password" or "Hide password" in the current language, and a language switch updates the names.

## Quick Examples

```xml
<!-- Variants -->
<controls:DaisyPasswordBox Watermark="Bordered (Default)" MaxLength="30" />
<controls:DaisyPasswordBox Variant="Ghost" Watermark="Ghost" MaxLength="30" />
<controls:DaisyPasswordBox Variant="Filled" Watermark="Filled" MaxLength="30" />
<controls:DaisyPasswordBox Variant="Primary" Watermark="Primary" MaxLength="30" />
<controls:DaisyPasswordBox Variant="Error" Watermark="Error" MaxLength="30" />

<!-- Floating labels, with and without an icon -->
<controls:DaisyPasswordBox Label="Password" LabelPosition="Floating"
                           Watermark="Enter password..." MaxLength="30" />
<controls:DaisyPasswordBox Label="Secret Key" LabelPosition="Floating"
                           StartIcon="{StaticResource DaisyIconSettings}"
                           Watermark="Enter secret key..." MaxLength="30" />

<!-- Sizes -->
<controls:DaisyPasswordBox Size="ExtraSmall" Watermark="Extra Small" MaxLength="30" />
<controls:DaisyPasswordBox Size="Small" Watermark="Small" MaxLength="30" />
<controls:DaisyPasswordBox Size="Medium" Watermark="Medium (default)" MaxLength="30" />
<controls:DaisyPasswordBox Size="Large" Watermark="Large" MaxLength="30" />

<!-- Mask, eye button, required and optional -->
<controls:DaisyPasswordBox Watermark="Custom mask (*)" PasswordChar="*" MaxLength="30" />
<controls:DaisyPasswordBox Watermark="No reveal button" IsPasswordRevealButtonEnabled="False" MaxLength="30" />
<controls:DaisyPasswordBox Label="Password" IsRequired="True" Watermark="Required field" MaxLength="30" />
<controls:DaisyPasswordBox Label="Optional Code" IsOptional="True" Watermark="Optional field" MaxLength="30" />

<!-- Bound to a view model, with the rules below the field -->
<controls:DaisyPasswordBox Label="New password" Password="{Binding NewPassword}"
                           HelperText="At least 12 characters" />
```

### PIN Code

The Gallery's PIN field shows a check icon once six characters are entered:

```xml
<controls:DaisyPasswordBox Label="PIN Code" LabelPosition="Floating"
                           PasswordChanged="OnPinCodeChanged"
                           Watermark="Enter PIN..." MaxLength="6" />
```

```csharp
private void OnPinCodeChanged(object? sender, RoutedEventArgs e)
{
    if (sender is DaisyPasswordBox pinBox)
    {
        // Show check icon when exactly 6 characters are entered
        if (pinBox.Password?.Length == 6)
        {
            pinBox.EndIcon = this.FindResource("DaisyIconCheck") as StreamGeometry;
        }
        else
        {
            pinBox.EndIcon = null;
        }
    }
}
```

## Tips & Best Practices

- Bind `Password` instead of reading the inner `TextBox`; it binds two-way without `Mode=TwoWay`.
- `MaxLength` limits only the length; the field accepts any character. For a code with one box per digit, use [DaisyOtpInput](DaisyOtpInput.md).
- Set `AutomationProperties.Name` when neither `Label` nor `Watermark` names the field, or screen readers announce only "Password".
- Keep the eye button on unless a policy forbids it; it lets users check what they typed.
- `controls:DaisyGlassSurface.IsEnabled="True"` puts the field on glass, like the other inputs (see [DaisyGlassSurface](DaisyGlassSurface.md)); the neumorphic mode shades the field itself ([DaisyNeumorphic](DaisyNeumorphic.md)).
