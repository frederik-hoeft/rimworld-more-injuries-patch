dotnet clean
dotnet restore --no-cache
dotnet build -c Release
dotnet publish -c Release -p:PublishProfile=release

$project_name = "MoreInjuriesPatch"
$upload_dir = "../../../../../../../common/RimWorld/Mods/${project_name}" 
$mod_root = "../../.."

# clean upload dir
if (Test-Path -LiteralPath $upload_dir) {
    Remove-Item -LiteralPath $upload_dir -Verbose -Recurse
}

# create new folder structure
New-Item -ItemType Directory $upload_dir
New-Item -ItemType Directory "${upload_dir}/Assemblies"
New-Item -ItemType Directory "${upload_dir}/Source"

# copy assemblies (deps should be handled via mod dependencies)
Copy-Item -LiteralPath "${mod_root}/Assemblies/${project_name}.dll" -Verbose -Destination "${upload_dir}/Assemblies"

# copy About
Copy-Item -LiteralPath "${mod_root}/About" -Recurse -Verbose -Destination $upload_dir –Container

# copy patches if exists
if (Test-Path -LiteralPath "${mod_root}/Patches") {
    Copy-Item -LiteralPath "${mod_root}/Patches" -Verbose -Recurse -Destination "${upload_dir}/Patches"
}

function Copy-Folder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [String]$FromPath,

        [Parameter(Mandatory)]
        [String]$ToPath,

        [string[]] $Exclude
    )

    if (Test-Path $FromPath -PathType Container) {
        New-Item $ToPath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        Get-ChildItem $FromPath -Force | ForEach-Object {
            # avoid the nested pipeline variable
            $item = $_
            $target_path = Join-Path $ToPath $item.Name
            if (($Exclude | ForEach-Object { $item.Name -like $_ }) -notcontains $true) {
                if (Test-Path $target_path) { Remove-Item $target_path -Recurse -Force }
                Copy-Item $item.FullName $target_path
                Copy-Folder -FromPath $item.FullName $target_path $Exclude
            }
        }
    }
}

# copy Source (exclude sensitive/unnecessary items)
Copy-Folder -FromPath "${mod_root}/Source" -ToPath "${upload_dir}/Source" -Exclude ".vs","bin","obj","*.user"

# copy README because why not
Copy-Item -LiteralPath "${mod_root}/README.md" -Destination $upload_dir

Write-Host "========== Deployment succeeded =========="