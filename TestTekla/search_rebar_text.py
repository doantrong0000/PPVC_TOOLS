import os

def search_files(directory, search_text):
    print(f"Searching for '{search_text}' in: {directory}...")
    found_files = []
    
    # Iterate through all directories and subfiles
    for root, dirs, files in os.walk(directory):
        for file in files:
            # Only read files that are likely text (ignore binary if needed)
            if file.endswith(('.cs', '.xaml', '.txt', '.xml', '.py', '.json', '.config', '.inp')):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                        content = f.read()
                        if search_text.lower() in content.lower():
                            print(f"FOUND: {file_path}")
                            found_files.append(file_path)
                except Exception as e:
                    # Skip unreadable files
                    pass

    if not found_files:
        print("No files found containing that text.")
    else:
        print(f"\nTotal files matched: {len(found_files)}")

if __name__ == "__main__":
    print("--- TEKLA TEXT SEARCH TOOL ---")
    
    # 1. Input directory path (Default is current directory)
    default_dir = os.getcwd()
    print(f"Default directory: {default_dir}")
    target_dir = input("Enter directory path to search (or press Enter for default): ").strip()
    if not target_dir:
        target_dir = default_dir
    
    # 2. Input text to find
    default_text = "rebar sequence number"
    text_to_find = input(f"Enter text to search (default: '{default_text}'): ").strip()
    if not text_to_find:
        text_to_find = default_text
    
    # Execute search
    if os.path.exists(target_dir):
        search_files(target_dir, text_to_find)
    else:
        print(f"Error: Directory '{target_dir}' does not exist.")
