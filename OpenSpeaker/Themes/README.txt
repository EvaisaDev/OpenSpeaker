Custom themes
=============

Drop a WPF ResourceDictionary .xaml file in this folder and it will appear in
Settings as a selectable theme. The file name (without .xaml) is the theme name
shown in the list, e.g. "Midnight.xaml" -> "Midnight".

The easiest way to make one is to copy Dark.xaml or Light.xaml and change the
colours/brushes. A custom file with the same name as a built-in theme (Dark,
Light) overrides it.

A theme must define every resource key the app references. Start from one of the
bundled files so nothing is missing. If a theme fails to load, the app falls back
to the built-in Dark theme.
