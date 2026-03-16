//! headphone_ffi – C-callable FFI layer for Soundcore headphone control using openscq30-lib.
//!
//! Every exported function returns a heap-allocated C string (or null on
//! allocation failure).  The caller must release that string via
//! `openscq30_string_free`.

#![allow(non_snake_case)]

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::{Arc, Mutex};
use std::str::FromStr;

use once_cell::sync::Lazy;
use tokio::runtime::Runtime;

use openscq30_lib::{
    default_backends,
    DeviceModel,
    storage::OpenSCQ30Database,
    device::OpenSCQ30Device,
    settings::{Setting, SettingId, Value},
};
use macaddr::MacAddr6;

// ---------------------------------------------------------------------------
// Global Runtime & State
// ---------------------------------------------------------------------------

static RUNTIME: Lazy<Runtime> = Lazy::new(|| {
    tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
        .expect("Failed to create Tokio runtime")
});

struct GlobalState {
    device: Option<Arc<dyn OpenSCQ30Device + Send + Sync>>,
    model_name: String,
}

static STATE: Lazy<Mutex<GlobalState>> = Lazy::new(|| {
    Mutex::new(GlobalState {
        device: None,
        model_name: String::new(),
    })
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

fn alloc_cstr(s: &str) -> *mut c_char {
    CString::new(s)
        .or_else(|_| CString::new(""))
        .expect("empty string is valid")
        .into_raw()
}

unsafe fn cstr_to_string(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    CStr::from_ptr(ptr).to_str().ok().map(str::to_owned)
}

fn map_model_name(name: &str) -> DeviceModel {
    // Basic mapping based on user input or known strings.
    // Default to Q30 (SoundcoreA3028) if unknown or generic.
    // OpenSCQ30 uses internal enum names like SoundcoreA3028.
    // We try to match common marketing names too.
    match name {
        "Soundcore Q30" | "Soundcore Life Q30" | "Life Q30" => DeviceModel::SoundcoreA3028,
        "Soundcore Q35" | "Soundcore Life Q35" | "Life Q35" => DeviceModel::SoundcoreA3027,
        "Soundcore A3040" | "Space Q45" => DeviceModel::SoundcoreA3040,
        // Add more if needed, or default
        _ => DeviceModel::SoundcoreA3028,
    }
}

// ---------------------------------------------------------------------------
// FFI Exports
// ---------------------------------------------------------------------------

#[no_mangle]
pub extern "C" fn openscq30_supported_model_count() -> u32 {
    // Just return a static count of "main" models we support explicitly in the UI
    5
}

#[no_mangle]
pub extern "C" fn openscq30_device_model_name(index: u32) -> *mut c_char {
    let name = match index {
        0 => "Soundcore Life Q30",
        1 => "Soundcore Life Q35",
        2 => "Soundcore Q30",
        3 => "Soundcore Q35",
        4 => "Soundcore A3040",
        _ => "",
    };
    alloc_cstr(name)
}

#[no_mangle]
pub extern "C" fn openscq30_status_message() -> *mut c_char {
    let state = STATE.lock().unwrap();
    if state.device.is_some() {
        alloc_cstr("Connected")
    } else {
        alloc_cstr("Disconnected")
    }
}

#[no_mangle]
pub unsafe extern "C" fn openscq30_connect(
    mac_address: *const c_char,
    name: *const c_char,
) -> *mut c_char {
    let mac_str = match cstr_to_string(mac_address) {
        Some(s) => s,
        None => return alloc_cstr("Error: Null MAC"),
    };
    let name_str = cstr_to_string(name).unwrap_or_default();

    let mac: MacAddr6 = match mac_str.parse() {
        Ok(m) => m,
        Err(_) => return alloc_cstr("Error: Invalid MAC address format"),
    };

    let model = map_model_name(&name_str);

    // Perform connection in the runtime
    let result = RUNTIME.block_on(async move {
        let database = Arc::new(OpenSCQ30Database::new_in_memory().await.map_err(|e| e.to_string())?);
        
        // We need default backends (Windows/Linux)
        let backends = default_backends()
            .ok_or_else(|| "No bluetooth backend available".to_string())?;

        let registry = model.device_registry(&backends, database)
            .await
            .map_err(|e| format!("Registry error: {}", e))?;

        let device = registry.connect(mac)
            .await
            .map_err(|e| format!("Connection failed: {}", e))?;
            
        Ok::<_, String>(device)
    });

    match result {
        Ok(device) => {
            let mut state = STATE.lock().unwrap();
            state.device = Some(device);
            state.model_name = name_str.clone();
            alloc_cstr(&format!("Connected to {}", name_str))
        }
        Err(e) => alloc_cstr(&format!("Error: {}", e)),
    }
}

#[no_mangle]
pub extern "C" fn openscq30_demo_connect() -> *mut c_char {
    // For demo, we can just connect to a "fake" device logic if openscq30 supports it,
    // or just return success and mock state. 
    // openscq30 has `demo_device_registry`.
    
    let result = RUNTIME.block_on(async move {
        let database = Arc::new(OpenSCQ30Database::new_in_memory().await.map_err(|e| e.to_string())?);
        let model = DeviceModel::SoundcoreA3028; // Q30 demo
        let registry = model.demo_device_registry(database)
            .await
            .map_err(|e| format!("Demo registry error: {}", e))?;
            
        // Use nil/demo mac
        let mac = model.demo_mac_address();
        let device = registry.connect(mac)
            .await
            .map_err(|e| format!("Demo connect error: {}", e))?;
            
        Ok::<_, String>(device)
    });

    match result {
        Ok(device) => {
            let mut state = STATE.lock().unwrap();
            state.device = Some(device);
            state.model_name = "Demo Q30".to_string();
            alloc_cstr("Connected to Demo Q30")
        }
        Err(e) => alloc_cstr(&format!("Error: {}", e)),
    }
}

#[no_mangle]
pub extern "C" fn openscq30_get_ambient_sound_mode() -> *mut c_char {
    let state = STATE.lock().unwrap();
    if let Some(device) = &state.device {
        // We need to read the setting. Since FFI is sync, we block on runtime.
        // But `device` methods are async? No, `setting` is sync.
        // Wait, `device.setting(id)` returns Option<Setting>.
        // BUT, the device state is updated via `watch_for_changes` or initially.
        // openscq30-lib usually fetches initial state on connect.
        
        let setting = device.setting(&SettingId::AmbientSoundMode);
        if let Some(Setting::Select { value, .. }) = setting {
             return alloc_cstr(&value);
        }
        alloc_cstr("Unknown")
    } else {
        alloc_cstr("Error: No device connected")
    }
}

#[no_mangle]
pub extern "C" fn openscq30_get_device_snapshot() -> *mut c_char {
    let state = STATE.lock().unwrap();
    let device = match &state.device {
        Some(d) => d,
        None => return alloc_cstr("Error: No device connected"),
    };

    // Helper to get string value
    let get_str = |id: SettingId| -> String {
        match device.setting(&id) {
            Some(Setting::Select { value, .. }) => value.to_string(),
            Some(Setting::OptionalSelect { value, .. }) => value.unwrap_or_default().to_string(),
            Some(Setting::Information { value, .. }) => value,
            _ => String::new(),
        }
    };
    
    // Helper to get int/bool (-1 if not present)
    let get_int = |id: SettingId| -> i32 {
        match device.setting(&id) {
            Some(Setting::I32Range { value, .. }) => value,
            Some(Setting::Toggle { value }) => if value { 1 } else { 0 },
            Some(Setting::Information { value, .. }) => {
                // Try to parse simple integer
                if let Ok(v) = value.parse::<i32>() {
                    return v;
                }
                // Handle "true"/"false"
                if value.to_lowercase() == "true" { return 1; }
                if value.to_lowercase() == "false" { return 0; }
                
                // Handle "x/y" battery format
                if value.contains('/') {
                    let parts: Vec<&str> = value.split('/').collect();
                    if parts.len() == 2 {
                        let cur = parts[0].trim().parse::<f32>().unwrap_or(0.0);
                        let max = parts[1].trim().parse::<f32>().unwrap_or(1.0);
                        if max <= 0.0 { return 0; }
                        return ((cur / max) * 100.0) as i32;
                    }
                }
                0
            }
            None => -1, // Not present on this device
            _ => 0,
        }
    };

    let model = &state.model_name;
    let ambient = get_str(SettingId::AmbientSoundMode);
    let anc_mode = get_str(SettingId::NoiseCancelingMode);
    let trans_mode = get_str(SettingId::TransparencyMode);

    let battery = get_int(SettingId::BatteryLevel);
    let battery_left = get_int(SettingId::BatteryLevelLeft);
    let battery_right = get_int(SettingId::BatteryLevelRight);
    let battery_case = get_int(SettingId::CaseBatteryLevel);
    let charging = get_int(SettingId::IsCharging) != 0;
    let charging_left = get_int(SettingId::IsChargingLeft) != 0;
    let charging_right = get_int(SettingId::IsChargingRight) != 0;

    let s = format!(
        "status=Connected|model={}|ambient={}|ancMode={}|transMode={}|battery={}|batteryLeft={}|batteryRight={}|batteryCase={}|charging={}|chargingLeft={}|chargingRight={}",
        model, ambient, anc_mode, trans_mode, battery, battery_left, battery_right, battery_case, charging, charging_left, charging_right
    );
    alloc_cstr(&s)
}

#[no_mangle]
pub unsafe extern "C" fn openscq30_set_ambient_sound_mode(
    mode: *const c_char,
) -> *mut c_char {
    set_device_setting(SettingId::AmbientSoundMode, mode)
}

#[no_mangle]
pub unsafe extern "C" fn openscq30_set_noise_canceling_mode(
    mode: *const c_char,
) -> *mut c_char {
    set_device_setting(SettingId::NoiseCancelingMode, mode)
}

#[no_mangle]
pub unsafe extern "C" fn openscq30_set_transparency_mode(
    mode: *const c_char,
) -> *mut c_char {
    set_device_setting(SettingId::TransparencyMode, mode)
}

unsafe fn set_device_setting(id: SettingId, val_ptr: *const c_char) -> *mut c_char {
    let val_str = match cstr_to_string(val_ptr) {
        Some(s) => s,
        None => return alloc_cstr("Error: Null value"),
    };

    let state = STATE.lock().unwrap();
    let device = match &state.device {
        Some(d) => d.clone(),
        None => return alloc_cstr("Error: No device connected"),
    };
    
    let result = RUNTIME.block_on(async move {
        device.set_setting_values(vec![
            (id, Value::from(std::borrow::Cow::Owned(val_str)))
        ]).await
    });

    match result {
        Ok(_) => alloc_cstr("Success"),
        Err(e) => alloc_cstr(&format!("Error: {}", e)),
    }
}

#[no_mangle]
pub extern "C" fn openscq30_disconnect() -> *mut c_char {
    let mut state = STATE.lock().unwrap();
    state.device = None;
    alloc_cstr("Disconnected")
}

#[no_mangle]
pub unsafe extern "C" fn openscq30_string_free(ptr: *mut c_char) {
    if !ptr.is_null() {
        drop(CString::from_raw(ptr));
    }
}
