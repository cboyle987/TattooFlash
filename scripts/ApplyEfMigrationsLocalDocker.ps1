$localDbCredential = Get-Credential -UserName 'tattooflash_user' -Message 'Local PostgreSQL container password'
$localDbConnection = New-Object System.Data.Common.DbConnectionStringBuilder
$localDbConnection['Host'] = '127.0.0.1'
$localDbConnection['Port'] = 5433
$localDbConnection['Database'] = 'tattooflash'
$localDbConnection['Username'] = $localDbCredential.UserName
$localDbConnection['Password'] = $localDbCredential.GetNetworkCredential().Password
$localDbConnection['GSS Encryption Mode'] = 'Disable'

$previousDbConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')
try {
    $env:ConnectionStrings__DefaultConnection = $localDbConnection.ConnectionString
    dotnet ef database update --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj
    if ($LASTEXITCODE -ne 0) {
        throw 'Applying local EF migrations failed; inspect the error above.'
    }
}
finally {
    if ($null -eq $previousDbConnection) {
        Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    }
    else {
        $env:ConnectionStrings__DefaultConnection = $previousDbConnection
    }
    Remove-Variable localDbCredential, localDbConnection, previousDbConnection
}