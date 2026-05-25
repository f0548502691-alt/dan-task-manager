#!/bin/bash
# 🚀 Getting Started Script for DanTaskManager

echo "=========================================="
echo "DanTaskManager - Strategy Pattern Setup"
echo "=========================================="
echo ""

# Step 1: Restore packages
echo "📦 Step 1: Restoring packages..."
dotnet restore
echo ""

# Step 2: Build
echo "🏗️ Step 2: Building project..."
dotnet build
echo ""

# Step 3: Run tests
echo "🧪 Step 3: Running unit tests..."
dotnet test
echo ""

# Step 4: Create migration
echo "🗄️ Step 4: Creating database migration..."
dotnet ef migrations add InitialCreate
echo ""

# Step 5: Update database
echo "📝 Step 5: Updating database..."
dotnet ef database update
echo ""

echo "=========================================="
echo "✅ Setup complete! Ready to run."
echo "=========================================="
echo ""
echo "📌 Next step: dotnet run"
echo ""
echo "📚 Documentation:"
echo "   - STRATEGY_PATTERN_DOCS.md   (Comprehensive guide)"
echo "   - QUICK_GUIDE.md             (Quick reference)"
echo "   - STRATEGY_EXAMPLES.cs       (Code examples)"
echo ""
