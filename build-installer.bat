@echo off
echo MMG 설치 파일 빌드 시작...
echo.

REM 1단계: Release 빌드
echo 1단계: Release 빌드 중...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
if %ERRORLEVEL% neq 0 (
    echo 빌드 실패!
    pause
    exit /b 1
)
echo Release 빌드 완료!
echo.

REM 2단계: installer 디렉토리 생성
if not exist "installer" mkdir installer

REM 3단계: Inno Setup 컴파일 (Inno Setup이 설치되어 있어야 함)
echo 2단계: Inno Setup으로 설치 파일 생성 중...
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "MMG-Setup.iss"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    "C:\Program Files\Inno Setup 6\ISCC.exe" "MMG-Setup.iss"
) else (
    echo Inno Setup이 설치되어 있지 않습니다.
    echo https://jrsoftware.org/isinfo.php 에서 Inno Setup을 다운로드하여 설치하세요.
    echo.
    echo 설치 후 다시 실행하세요.
    pause
    exit /b 1
)

if %ERRORLEVEL% neq 0 (
    echo 설치 파일 생성 실패!
    pause
    exit /b 1
)

echo.
echo 설치 파일 생성 완료!
echo 출력 위치: installer\MMG-Setup-v1.0.0.exe
echo.
pause