using System.Runtime.CompilerServices;

// Counter-limit tests construct otherwise unreachable revisions without a public state/receipt injection API.
[assembly: InternalsVisibleTo("Zodiac.Framework.Tests")]
