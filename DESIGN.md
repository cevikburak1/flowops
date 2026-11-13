# FlowOps design system

FlowOps puts the purchase ledger at the center of the workspace. A dark navy navigation rail anchors the light theme. The workspace uses cool neutral surfaces and one blue accent for action and selection. The dark theme uses a lower-contrast surface hierarchy and a lighter blue accent.

- Geist Variable carries controls and content. Use tabular numerals for amounts and counts.
- Keep the table readable at normal zoom. On narrow screens use native horizontal scrolling within the table.
- Show the request context and decision controls in the main column with a timestamped activity panel alongside. Stack them on mobile.
- Use semantic status labels and reserve green, amber, and red for decision state. Keep the text labels present in both themes.
- The sidebar collapses below 960px into a keyboard-operable menu. English and Turkish use the same controls and layout.
- Keep motion limited to short interaction feedback and honor reduced-motion preferences.
- The purchase workflow canvas uses the workspace palette, with labeled branches and a form for editing connections by keyboard. On narrow screens, use the zoom and fit controls to inspect the graph.

The implemented theme tokens and breakpoints live in [globals.css](apps/web/app/globals.css).
