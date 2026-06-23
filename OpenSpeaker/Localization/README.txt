Custom languages
================

Drop a WPF ResourceDictionary .xaml file in this folder and it will appear in
Settings as a selectable language. The file name (without .xaml) is the language
name shown in the list, e.g. "Francais.xaml" -> "Francais".

The easiest way to make one is to copy English.xaml and translate the string
values, keeping every x:Key the same. A custom file with the same name as a
built-in language (English) overrides it.

If a language fails to load, the app falls back to the built-in English strings.
