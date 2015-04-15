
[CmdletBinding()]

param($SolutionDir, $ProjectDir, $TargetDir, $TargetFileName, $ConfigurationName, $nuspec, [switch]$Disable)

# Regular expression pattern to find the version in the build number 
# and then apply it to the assemblies
$VersionRegex = "\d+\.\d+\.\d+(\.\d+)?(\.\w+)?"

if(-not ($SolutionDir -and $ProjectDir -and $TargetDir -and $TargetFileName -and $ConfigurationName))
{
	Write-Error "SolutionDir, ProjectDir, TargetDir, TargetFileName and ConfigurationName are all required"
	exit 1
}

if ($PSBoundParameters.ContainsKey('Disable'))
{
	Write-Verbose "Script disabled; no actions will be taken on the files."
}

# read the desired new version
[xml] $doc = gc $SolutionDir"Version.xml"
$NewVersion = $doc.Version.Major + "." + $doc.Version.Minor + "." + $doc.Version.Build + "." + $doc.Version.Revision
$NewNuspecVersion = $doc.Version.Major + "." + $doc.Version.Minor + "." + $doc.Version.Build + $doc.Version.Suffix

# Apply the version to the assembly property files
$files = gci $ProjectDir -recurse -include "*Properties*","My Project" | 
	?{ $_.PSIsContainer } | 
	foreach { gci -Path $_.FullName -Recurse -include AssemblyInfo.* }
if($files)
{
	Write-Verbose "Will apply $NewVersion to $($files.count) files."
	
	foreach ($file in $files) {
		if(-not $Disable)
		{
			$filecontent = Get-Content($file)
			attrib $file -r
			$filecontent -replace $VersionRegex, $NewVersion | Out-File $file
			Write-Verbose "$file - version applied"
		}
	}
}
else
{
	Write-Warning "Found no files."
}

# Apply the version to the nuspec files
if($nuspec)
{
	Write-Verbose "Will apply $NewNuspecVersion to $nuspec"
	
	if(-not $Disable)
	{
		[xml] $nuspecDoc = gc $nuspec
		$nuspecDoc.package.metadata.version = $NewNuspecVersion
		$nuspecDoc.Save($nuspec)
		Write-Verbose "$nuspec - version applied"
	}
}
else
{
	Write-Warning "Found no nuspec files."
}
