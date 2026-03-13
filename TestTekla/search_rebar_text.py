import os

def search_files(directory, search_text):
    print(f"Searching for '{search_text}' in: {directory}...")
    found_files = []
    
    # Duyệt qua toàn bộ thư mục và file con
    for root, dirs, files in os.walk(directory):
        for file in files:
            # Chỉ đọc các file có khả năng là text (bỏ qua binary nếu cần)
            if file.endswith(('.cs', '.xaml', '.txt', '.xml', '.py', '.json', '.config', '.inp')):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                        content = f.read()
                        if search_text.lower() in content.lower():
                            print(f"FOUND: {file_path}")
                            found_files.append(file_path)
                except Exception as e:
                    # Bỏ qua các file không đọc được
                    pass

    if not found_files:
        print("No files found containing that text.")
    else:
        print(f"\nTotal files matched: {len(found_files)}")

if __name__ == "__main__":
    print("--- TEKLA TEXT SEARCH TOOL ---")
    
    # 1. Nhập đường dẫn thư mục (Mặc định là thư mục hiện tại)
    default_dir = os.getcwd()
    print(f"Default directory: {default_dir}")
    target_dir = input("Enter directory path to search (or press Enter for default): ").strip()
    if not target_dir:
        target_dir = default_dir
    
    # 2. Nhập nội dung cần tìm
    default_text = "rebar sequence number"
    text_to_find = input(f"Enter text to search (default: '{default_text}'): ").strip()
    if not text_to_find:
        text_to_find = default_text
    
    # Thực hiện tìm kiếm
    if os.path.exists(target_dir):
        search_files(target_dir, text_to_find)
    else:
        print(f"Error: Directory '{target_dir}' does not exist.")
