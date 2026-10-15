"""Shared output routes for documentation pages and indexes."""

GUIDE_FILES = (
    'MigrationExample.md', 'DesignTokens.md', 'Effects.md', 'SizingScaling.md',
    'working_with_animations.md',
)

NON_API_FILES = {'README.md', 'WORKFLOW.md', *GUIDE_FILES}


def page_manifest(control_names, guide_names):
    pages = {name: f'controls/{name}.html' for name in control_names}
    pages.update({name.removesuffix('.md'): name.removesuffix('.md') + '.html' for name in guide_names})
    return pages
