# This repository has moved!

This repository has been merged into https://github.com/NuGet/NuGetGallery. All changes to the nuget.org jobs, V3 pipeline, and
Search Service should be made to the NuGetGallery repository.

As of NuGetGallery commit
[`d55ed52c78a34c5416ecd6734af8e1aad0d97765`](https://github.com/NuGet/NuGetGallery/commit/d55ed52c78a34c5416ecd6734af8e1aad0d97765)
all projects, files, and Git history of the NuGet.Jobs repository have been merged into the NuGetGallery
repository. This merge was done to reduce the amount of overhead needed to maintain the back-end of nuget.org.

If you have a broken link found in documentation, please let us know by opening a bug on that documentation page.

If you'd like to try to work around the broken link, try changing the "NuGet.Jobs" part of the URL to
"NuGetGallery". This is not guaranteed to work as the code changes, but it may help. For example:

<pre>
BEFORE: https://github.com/NuGet/<b>NuGet.Jobs</b>/blob/dev/build.ps1
 AFTER: https://github.com/NuGet/<b>NuGetGallery</b>/blob/dev/build.ps1
</pre>

## Perhaps you're looking for...

- [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery) -  the destination for this repository move, the code that runs the www.nuget.org website and the issue tracker for all nuget.org issues
- [NuGet/Home](https://github.com/NuGet/Home) - the issue tracker for NuGet client
- [NuGet/NuGet.Client](https://github.com/NuGet/NuGet.Client) - the code for NuGet client, i.e. Visual Studio
  integration, .NET CLI integration, MSBuild integration, nuget.exe, etc. 

## Still confused?

Feel free to open an issue at [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery/issues) and someone will help
you out.
