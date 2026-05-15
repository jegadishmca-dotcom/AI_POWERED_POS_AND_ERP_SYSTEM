$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

# Backend folders
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Api"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure"

# Domain csproj
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\PosErp.Domain.csproj" -Encoding utf8

# Application csproj
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PosErp.Domain\PosErp.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="FluentValidation" Version="11.9.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
    <PackageReference Include="Mapster" Version="7.4.0" />
  </ItemGroup>
</Project>
"@ | Out-File -FilePath "$backendDir\PosErp.Application\PosErp.Application.csproj" -Encoding utf8

# Infrastructure csproj
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PosErp.Application\PosErp.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.4" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.2" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.33" />
    <PackageReference Include="Hangfire.AspNetCore" Version="1.8.12" />
    <PackageReference Include="Hangfire.PostgreSql" Version="1.20.8" />
    <PackageReference Include="QuestPDF" Version="2024.3.4" />
  </ItemGroup>
</Project>
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\PosErp.Infrastructure.csproj" -Encoding utf8

# Api csproj
@"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PosErp.Application\PosErp.Application.csproj" />
    <ProjectReference Include="..\PosErp.Infrastructure\PosErp.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.1" />
    <PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.4" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
  </ItemGroup>
</Project>
"@ | Out-File -FilePath "$backendDir\PosErp.Api\PosErp.Api.csproj" -Encoding utf8

# Minimal SLN file using .NET format
@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PosErp.Api", "PosErp.Api\PosErp.Api.csproj", "{8562A7D8-9C6D-4D3B-8C4E-6666579E7B34}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PosErp.Application", "PosErp.Application\PosErp.Application.csproj", "{B858B4DE-6C8E-4A42-9988-9128D38AE122}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PosErp.Domain", "PosErp.Domain\PosErp.Domain.csproj", "{D1F6E2DB-1F3B-49DF-8B6C-728DF80A12A6}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PosErp.Infrastructure", "PosErp.Infrastructure\PosErp.Infrastructure.csproj", "{3C8B7A46-2C28-43BA-85BA-E3B0DF818815}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{8562A7D8-9C6D-4D3B-8C4E-6666579E7B34}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8562A7D8-9C6D-4D3B-8C4E-6666579E7B34}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8562A7D8-9C6D-4D3B-8C4E-6666579E7B34}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8562A7D8-9C6D-4D3B-8C4E-6666579E7B34}.Release|Any CPU.Build.0 = Release|Any CPU
		{B858B4DE-6C8E-4A42-9988-9128D38AE122}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B858B4DE-6C8E-4A42-9988-9128D38AE122}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B858B4DE-6C8E-4A42-9988-9128D38AE122}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B858B4DE-6C8E-4A42-9988-9128D38AE122}.Release|Any CPU.Build.0 = Release|Any CPU
		{D1F6E2DB-1F3B-49DF-8B6C-728DF80A12A6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D1F6E2DB-1F3B-49DF-8B6C-728DF80A12A6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D1F6E2DB-1F3B-49DF-8B6C-728DF80A12A6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D1F6E2DB-1F3B-49DF-8B6C-728DF80A12A6}.Release|Any CPU.Build.0 = Release|Any CPU
		{3C8B7A46-2C28-43BA-85BA-E3B0DF818815}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{3C8B7A46-2C28-43BA-85BA-E3B0DF818815}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{3C8B7A46-2C28-43BA-85BA-E3B0DF818815}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{3C8B7A46-2C28-43BA-85BA-E3B0DF818815}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
"@ | Out-File -FilePath "$backendDir\PosErp.sln" -Encoding utf8

# Frontend structure
New-Item -ItemType Directory -Force -Path "$frontendDir\src\components"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\store"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\db"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\hooks"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\utils"
New-Item -ItemType Directory -Force -Path "$frontendDir\public"

# package.json
@"
{
  "name": "pos-erp-frontend",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "lint": "eslint . --ext ts,tsx --report-unused-disable-directives --max-warnings 0",
    "preview": "vite preview"
  },
  "dependencies": {
    "@hookform/resolvers": "^3.3.4",
    "@tanstack/react-query": "^5.28.9",
    "dexie": "^4.0.1",
    "dexie-react-hooks": "^1.1.7",
    "lucide-react": "^0.363.0",
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "react-hook-form": "^7.51.2",
    "react-intl": "^6.6.4",
    "react-router-dom": "^6.22.3",
    "zod": "^3.22.4",
    "zustand": "^4.5.2"
  },
  "devDependencies": {
    "@types/react": "^18.2.66",
    "@types/react-dom": "^18.2.22",
    "@typescript-eslint/eslint-plugin": "^7.2.0",
    "@typescript-eslint/parser": "^7.2.0",
    "@vitejs/plugin-react": "^4.2.1",
    "autoprefixer": "^10.4.19",
    "eslint": "^8.57.0",
    "eslint-plugin-react-hooks": "^4.6.0",
    "eslint-plugin-react-refresh": "^0.4.6",
    "postcss": "^8.4.38",
    "tailwindcss": "^3.4.3",
    "typescript": "^5.2.2",
    "vite": "^5.2.0"
  }
}
"@ | Out-File -FilePath "$frontendDir\package.json" -Encoding utf8

# vite.config.ts
@"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
})
"@ | Out-File -FilePath "$frontendDir\vite.config.ts" -Encoding utf8

# tsconfig.json
@"
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
"@ | Out-File -FilePath "$frontendDir\tsconfig.json" -Encoding utf8

# tsconfig.node.json
@"
{
  "compilerOptions": {
    "composite": true,
    "skipLibCheck": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true,
    "strict": true
  },
  "include": ["vite.config.ts"]
}
"@ | Out-File -FilePath "$frontendDir\tsconfig.node.json" -Encoding utf8

# index.html
@"
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/vite.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Enterprise Supermarket POS & ERP</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
"@ | Out-File -FilePath "$frontendDir\index.html" -Encoding utf8

# tailwind.config.js
@"
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
"@ | Out-File -FilePath "$frontendDir\tailwind.config.js" -Encoding utf8

# postcss.config.js
@"
export default {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
}
"@ | Out-File -FilePath "$frontendDir\postcss.config.js" -Encoding utf8

# src/main.tsx
@"
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
"@ | Out-File -FilePath "$frontendDir\src\main.tsx" -Encoding utf8

# src/index.css
@"
@tailwind base;
@tailwind components;
@tailwind utilities;
"@ | Out-File -FilePath "$frontendDir\src\index.css" -Encoding utf8

# src/App.tsx
@"
function App() {
  return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center">
      <h1 className="text-4xl font-bold text-blue-600">Enterprise POS & ERP System</h1>
    </div>
  )
}

export default App
"@ | Out-File -FilePath "$frontendDir\src\App.tsx" -Encoding utf8

Write-Host "Scaffolding Complete!"
