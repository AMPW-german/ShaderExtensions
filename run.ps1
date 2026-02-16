dotnet build

Push-Location "D:\programms\StarMap"
try {
  .\StarMap.exe
} finally {
  Pop-Location
}
