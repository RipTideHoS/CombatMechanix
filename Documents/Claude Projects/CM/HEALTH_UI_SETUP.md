# Health UI Setup Guide

This guide explains how to set up the health indicators for Combat Mechanix, including enemy health bars above enemies and player health display.

## Overview

The health UI system consists of several components:

1. **EnemyHealthBar.cs** - World-space health bars above enemies
2. **PlayerHealthUI.cs** - Enhanced player health display with animations
3. **HealthBarManager.cs** - Manages all enemy health bars automatically
4. **Updated UIManager.cs** - Integrates with existing UI system
5. **Updated AutoSceneSetup.cs** - Automatically creates the entire health system

## Quick Setup (Recommended)

### Automatic Setup via AutoSceneSetup

**This is the easiest way to set up the health system:**

1. **Find the AutoSceneSetup GameObject** in your scene (created when you run the game)
2. **In the Inspector**, ensure `Setup Health Bar System` is checked ✅
3. **Click "Setup Scene Now"** button or run the scene

**That's it!** The AutoSceneSetup will automatically create:
- Enemy health bar prefabs with proper UI components
- Player health bar in the main UI (top-left corner)
- HealthBarManager with optimized pooling
- All connections between systems

### What AutoSceneSetup Creates

When you run AutoSceneSetup with `SetupHealthBarSystem = true`, it automatically:

1. **Creates Enemy Health Bar Prefab** with:
   - World-space Canvas
   - Background, Slider, Fill, and Text components
   - EnemyHealthBar script with all references connected
   - Green→Yellow→Red color transitions

2. **Creates Player Health UI** with:
   - Main UI health bar (positioned top-left)
   - PlayerHealthUI script with animations enabled
   - Damage flash and low health warning features

3. **Sets up HealthBarManager** with:
   - Automatic enemy health bar management
   - Object pooling (10 initial, 30 max)
   - 50-unit view distance with occlusion checking
   - Auto-cleanup when enemies die

4. **Connects everything** to UIManager for backward compatibility

## Manual Setup Instructions (If Needed)

### 1. Enemy Health Bars

#### A. Create Enemy Health Bar Prefab

1. Create a new GameObject called "EnemyHealthBarPrefab"
2. Add a Canvas component with these settings:
   - Render Mode: World Space
   - Sorting Layer: UI (or higher)
3. Under the Canvas, create:
   - Background Image (dark/transparent)
   - Health Slider with:
     - Background Image
     - Fill Image (colored health bar)
   - Health Text (optional)

#### B. Setup EnemyHealthBar Component

1. Add the `EnemyHealthBar.cs` script to the prefab
2. Assign UI references:
   - HealthBarCanvas: The Canvas component
   - HealthSlider: The Slider component
   - HealthText: The Text component (if desired)
   - HealthFillImage: The Fill Image of the slider
   - BackgroundImage: The background image

3. Configure settings:
   - WorldOffset: Position above enemy (default: 0, 2, 0)
   - ShowHealthText: Whether to show numbers
   - ShowWhenFullHealth: Whether to show at full health
   - Colors: Full, mid, low health colors

#### C. Setup HealthBarManager

1. Create an empty GameObject called "HealthBarManager"
2. Add the `HealthBarManager.cs` script
3. Assign the EnemyHealthBarPrefab to the manager
4. Configure settings:
   - AutoManageEnemyHealthBars: true (recommended)
   - EnableHealthBarPooling: true for performance
   - InitialPoolSize: 10-20 depending on enemy count
   - MaxViewDistance: How far health bars are visible

### 2. Player Health Display

#### A. Enhanced Player Health UI

1. Find or create your main UI Canvas
2. Create player health UI elements:
   - Health Slider
   - Health Text
   - Health Fill Image
   - Background Image

#### B. Setup PlayerHealthUI Component

1. Add `PlayerHealthUI.cs` to your UI GameObject
2. Assign UI references similar to enemy health bars
3. Configure options:
   - ShowAbovePlayer: true if you want health above player too
   - AnimateHealthChanges: true for smooth transitions
   - ShowDamageFlash: true for visual feedback
   - EnableLowHealthWarning: true for pulsing at low health

#### C. Update UIManager

The UIManager has been updated to integrate with the new health system:
1. Assign the PlayerHealthUI component to UIManager
2. The system will automatically use existing HealthBar and HealthText if available
3. Health changes from ClientPlayerStats will update both old and new UI elements

### 3. Integration with Existing Systems

#### A. EnemyBase Integration

The health bars automatically connect to `EnemyBase.cs` through events:
- `OnHealthChanged`: Updates health bar display
- `OnEnemyDeath`: Hides/removes health bar

No additional setup needed - existing enemies will work automatically.

#### B. ClientPlayerStats Integration

Player health connects to `ClientPlayerStats.cs` events:
- `OnHealthChanged`: Animates health changes
- `OnStatsUpdated`: Updates full health display

The system works with your existing server-authoritative health system.

## Testing the Health System

### Using AutoSceneSetup (Easy Testing)

1. **Run the scene** with AutoSceneSetup configured
2. **Check the Console** for setup messages:
   ```
   === Setting up Health Bar System ===
   Enemy Health Bar Prefab created...
   HealthBarManager created and configured...
   Health Bar System setup complete!
   ```

3. **Test Enemy Health Bars:**
   - Enable `Create Test Enemy = true` in AutoSceneSetup
   - Run the scene again
   - You should see a red cube enemy with a health bar above it
   - In the Console, type: `FindObjectOfType<EnemyBase>().TakeDamage(25)`
   - Watch the health bar change from green to yellow/red

4. **Test Player Health:**
   - Look for the health bar in the top-left corner (shows "100/100")
   - In the Console, find ClientPlayerStats and test: `FindObjectOfType<ClientPlayerStats>().TestHealthChange(-20, "Console Test")`
   - Watch for smooth animation and color changes

### Manual Testing

1. **Enemy Health Bars:**
   - Spawn enemies with EnemyBase component
   - Health bars should appear automatically above enemies
   - Test damage with `enemy.TakeDamage(amount)` to see health updates

2. **Player Health:**
   - Use ClientPlayerStats test methods or server health changes
   - Health should animate smoothly with visual effects

## Customization Options

### Visual Styling

- Modify colors in both EnemyHealthBar and PlayerHealthUI components
- Adjust world offsets for health bar positioning
- Configure animation curves for smooth health transitions
- Set up low health warning effects

### Performance Optimization

- Adjust HealthBarManager pool sizes based on enemy count
- Set appropriate MaxViewDistance to limit rendered health bars
- Enable/disable occlusion checking based on scene complexity
- Adjust UpdateRate in HealthBarManager for performance vs responsiveness

### Multiplayer Considerations

- Health bars work with server-authoritative health from ClientPlayerStats
- Enemy health updates are handled locally but can sync with server
- All visual effects are client-side only for performance

## Troubleshooting

### Health Bars Not Appearing

1. Check HealthBarManager is active and has prefab assigned
2. Verify enemies have EnemyBase component with valid health
3. Ensure Camera.main is available for world-to-screen positioning
4. Check MaxViewDistance settings

### Player Health Not Updating

1. Verify ClientPlayerStats component exists and has events
2. Check PlayerHealthUI is assigned to UIManager
3. Ensure health slider and text references are set
4. Look for console errors regarding missing components

### Performance Issues

1. Reduce HealthBarManager pool sizes
2. Increase UpdateRate for less frequent updates
3. Lower MaxViewDistance to cull distant health bars
4. Disable occlusion checking if not needed

## File Locations

- `Client/Assets/Scripts/UI/EnemyHealthBar.cs`
- `Client/Assets/Scripts/UI/PlayerHealthUI.cs`
- `Client/Assets/Scripts/UI/HealthBarManager.cs`
- `Client/Assets/Scripts/Managers/UIManager.cs` (updated)

The health UI system is now ready for use in Combat Mechanix multiplayer environment!