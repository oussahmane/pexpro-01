#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            store_refresh_token,
            load_refresh_token,
            clear_refresh_token
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

fn credential() -> Result<keyring::Entry, ()> {
    keyring::Entry::new("com.pexilio.desktop", "refresh-token").map_err(|_| ())
}

#[tauri::command]
fn store_refresh_token(token: String) -> Result<(), String> {
    if token.is_empty() {
        return Err("unavailable".to_string());
    }

    credential()
        .map_err(|_| "unavailable".to_string())?
        .set_password(&token)
        .map_err(|_| "unavailable".to_string())
}

#[tauri::command]
fn load_refresh_token() -> Result<String, String> {
    credential()
        .map_err(|_| "unavailable".to_string())?
        .get_password()
        .map_err(|_| "unavailable".to_string())
}

#[tauri::command]
fn clear_refresh_token() -> Result<(), String> {
    match credential() {
        Ok(entry) => match entry.delete_credential() {
            Ok(()) | Err(keyring::Error::NoEntry) => Ok(()),
            Err(_) => Err("unavailable".to_string()),
        },
        Err(()) => Ok(()),
    }
}

#[cfg(test)]
mod credential_store_tests {
    use super::{clear_refresh_token, load_refresh_token, store_refresh_token};

    #[test]
    fn os_store_round_trip_clears_the_token() {
        use std::io::Read;

        let mut bytes = [0u8; 16];
        std::fs::File::open("/dev/urandom")
            .expect("urandom")
            .read_exact(&mut bytes)
            .expect("read");
        let token = bytes.iter().map(|byte| format!("{byte:02x}")).collect::<String>();
        store_refresh_token(token.clone()).expect("store");
        assert_eq!(load_refresh_token().expect("load"), token);
        clear_refresh_token().expect("clear");
        assert!(load_refresh_token().is_err());
    }
}
