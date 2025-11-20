# Blazor Render Modes in DevBuddy

## Overview

DevBuddy uses Blazor Server with interactive rendering for its UI components. This document explains the render mode pattern used in the application.

## Default Behavior

In Blazor 8.0+, components are rendered as **static server-side rendered (SSR)** by default. This means:
- No client interactivity (e.g., `@onclick` handlers don't work)
- No state management between renders
- No real-time updates via SignalR

## Making Pages Interactive

To enable interactivity on a page, add the `@rendermode InteractiveServer` directive at the top of the Razor file:

```razor
@page "/ui/my-page"
@rendermode InteractiveServer
```

### Simplified Syntax

The `_Imports.razor` file includes `@using static Microsoft.AspNetCore.Components.Web.RenderMode`, which allows the simpler syntax:

```razor
@rendermode InteractiveServer
```

Instead of the verbose:

```razor
@rendermode @(new Microsoft.AspNetCore.Components.Web.InteractiveServerRenderMode())
```

## When to Use Interactive Mode

Add `@rendermode InteractiveServer` when your page needs:

- Event handlers (e.g., `@onclick`, `@onchange`)
- Two-way data binding (e.g., `@bind`)
- State management (e.g., variables that change based on user interaction)
- Real-time updates from the server

## When NOT to Use Interactive Mode

For purely static pages with no user interaction (like the Home page), you can omit the `@rendermode` directive to improve performance.

## Examples

### Interactive Page (Git Repos)

```razor
@page "/ui/git-repos"
@rendermode InteractiveServer
@using DevBuddy.Server.Data.Models
@using DevBuddy.Server.Services
@inject IGitRepositoryService GitRepoService

<!-- Interactive elements work here -->
<button @onclick="ShowAddModal">Add Repository</button>
```

### Static Page (Home)

```razor
@page "/ui"

<!-- No @rendermode needed - this is a static page -->
<h1>Welcome to DevBuddy</h1>
<a href="/ui/git-repos">Get Started</a>
```

## Best Practices

1. **Add `@rendermode InteractiveServer` to any page with user interactions** - This prevents bugs where buttons and forms don't work.

2. **Keep static pages static** - Don't add `@rendermode` to pages that don't need it for better performance.

3. **Document interactive components** - When creating a new page, clearly indicate in comments whether it needs interactivity.

4. **Test after adding new pages** - Always test that buttons, forms, and other interactive elements work as expected.

## Troubleshooting

### Problem: Button clicks don't work

**Solution:** Add `@rendermode InteractiveServer` to the top of your page.

### Problem: Form inputs don't update state

**Solution:** Ensure the page has `@rendermode InteractiveServer` and uses `@bind` correctly.

### Problem: Modal doesn't appear

**Solution:** The component likely needs `@rendermode InteractiveServer` for the state management to work.

## Reference

- [Blazor Render Modes Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes)
- [ASP.NET Core Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
