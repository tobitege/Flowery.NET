# Overview

`DaisyLoginButton` provides localized login buttons with brand icons and optional brand colors. Supported brands are Email, GitHub, Google, Facebook, X, Kakao, Apple, Amazon, Microsoft, Line, Slack, LinkedIn, VK, WeChat, and MetaMask.

## Examples

```xml
<controls:DaisyLoginButton Brand="Google" />
<controls:DaisyLoginButton Brand="GitHub" Size="Small" />
<controls:DaisyLoginButton Brand="Microsoft" LoginText="Continue with Microsoft" />
<controls:DaisyLoginButton Brand="Email" UseBrandColors="False" Variant="Primary" />
```

`LoginText` overrides the localized default label. `IconSize` overrides the size derived from the button's `Size`; inherited `IconSpacing` can be set explicitly when custom spacing is required.
