@echo off
REM 🚀 Getting Started Script for DanTaskManager (Windows)

echo.
echo ==========================================
echo DanTaskManager - Strategy Pattern Setup
echo ==========================================
echo.

REM Step 1: Restore packages
echo 📦 Step 1: Restoring packages...
call dotnet restore
echo.

REM Step 2: Build
echo 🏗️ Step 2: Building project...
call dotnet build
echo.

REM Step 3: Run tests
echo 🧪 Step 3: Running unit tests...
call dotnet test
echo.

REM Step 4: Create migration
echo 🗄️ Step 4: Creating database migration...
call dotnet ef migrations add InitialCreate
echo.

REM Step 5: Update database
echo 📝 Step 5: Updating database...
call dotnet ef database update
echo.

echo.
echo ==========================================
echo ✅ Setup complete! Ready to run.
echo ==========================================
echo.
echo 📌 Next step: dotnet run
echo.
echo 📚 Documentation:
echo    - STRATEGY_PATTERN_DOCS.md   (Comprehensive guide)
echo    - QUICK_GUIDE.md             (Quick reference)
echo    - STRATEGY_EXAMPLES.cs       (Code examples)
echo.
pause
