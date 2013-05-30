PdbTasks
========

MSBuild tasks for PDB files

Installation
------------
1. Install `PdbTasks` from NuGet
2. Include tasks into your csproj file `<Import Project="..\packages\PdbTasks.1.0\tools\PdbTasks.targets" />`
3. Use tasks

Requirement
-----------
- [Debugging Tools for Windows](http://msdn.microsoft.com/en-us/library/windows/hardware/ff551063\(v=vs.85\).aspx)
- Command-line SVN client

Example usage
-------------
You may include following code into your csproj

	<Import Project="..\packages\PdbTasks.1.0\tools\PdbTasks.targets" />
	<ItemGroup>
		<PdbFile Include="$(OutputPath)\$(AssemblyName).pdb"/>
	</ItemGroup>
	<ItemGroup>
		<SymbolFiles Include="$(OutputPath)\*.pdb"/>
		<SymbolFiles Include="$(OutputPath)\*.exe"/>
		<SymbolFiles Include="$(OutputPath)\*.dll"/>
	</ItemGroup>
	<Target Name="AfterBuild">
		<PdbIndexFromSvn SourcePdb="@(PdbFile)" SourceDirectory="." Condition="$(SymbolServer) != ''"/>
		<PdbUploadToSymbolServer Files="@(SymbolFiles)" SymbolServer="$(SymbolServer)" Condition="$(SymbolServer) != ''"/>
	</Target>

Now you can build your project in regular manner to debug locally or add build property `SymbolServer` to instrument your pdb and upload artifacts to symbol server.

Task: PdbIndexFromSvn
---------------------
*Description:* Index given pdb to reference sources in SVN

*Usage:* `<PdbIndexFromSvn SourcePdb="file reference" SourceDirectory="path to source">`

*Arguments:*

- **SourcePdb** - reference to indexed
- **SourceDirectory** - path to sources
- **UserName** - SVN user name for getting sources
- **Password** - SVN password for getting sources
- **DebugToolsPath** - Debugging Tools for Windows installation path

Task: PdbUploadToSymbolServer
-----------------------------
*Description:* Upload artifacts to given symbol server

*Usage:* `<PdbIndexFromSvn Files="files reference" SymbolServer="symbol server path">`

*Arguments:*

- **Files** - files to upload (usually it's *.pdb, *.exe, *.dll)
- **SymbolServer** - path to symbol server
- **ProductName** - product name for symbol server
- **Version** - version for symbol server
- **Comment** - comment for symbol server
- **DebugToolsPath** - Debugging Tools for Windows installation path

LICENSE
=======

PdbTasks is available under the GNU LGPL version 3 or later, or - at your option -
the GNU GPL version 3 or later.

See [Licensing FAQ](http://eigen.tuxfamily.org/index.php?title=Licensing_FAQ&oldid=1116) for a
good description of what that means.

AUTHORS
=======

PdbTasks was mainly written and is maintained by Alik Kurdyukov <akurdyukov@gmail.com>.

Portions of code by SK Genius (http://www.codeproject.com/Members/SK-Genius).
