#include <windows.h>
#include <shlobj.h>
#include <wrl.h>
#include <iostream>
#include <string>
#include "CommandLine.h"
#include "TestFolderSite.h"
using Microsoft::WRL::ComPtr;
using GetFactory = HRESULT(__stdcall*)(REFCLSID, REFIID, void**);
using CanUnload = HRESULT(__stdcall*)();
const CLSID CommandId = {0xc419d0d4,0x39ca,0x43a8,{0x9b,0x43,0x75,0x51,0x4b,0xab,0x6b,0x78}};
static void Check(bool condition, const char* message) { if (!condition) throw message; }
static void CheckHr(HRESULT hr, const char* message)
{
    if (FAILED(hr)) { std::cerr << "HRESULT 0x" << std::hex << static_cast<unsigned long>(hr) << std::dec << ": "; throw message; }
}
static void CheckQuoting()
{
    const std::wstring arguments[] = { L"", L"C:\\", L"C:\\space path\\", L"\\\\server\\share\\", L"C:\\a & b\\%PATH%\\(folder)", L"C:\\\u9a8c\u8bc1\\", L"embedded\"quote", L"slashes\\\\\"quote" };
    for (const auto& argument : arguments)
    {
        auto text = QuoteArgument(L"C:\\Program Files\\CreateFile.App.exe") + L" --folder " + QuoteArgument(argument);
        int count = 0; auto parsed = CommandLineToArgvW(text.c_str(), &count);
        const bool correct = parsed && count == 3 && std::wstring(parsed[1]) == L"--folder" && std::wstring(parsed[2]) == argument;
        LocalFree(parsed); Check(correct, "Windows argument round trip");
    }
    std::cout << "PASS 8 argument round trips (root, UNC, Unicode, spaces, quotes, shell metacharacters)\n";
}
static void CheckStateCases(IExplorerCommand* command, IShellItem* directory, const wchar_t* dllPath, bool registered)
{
    EXPCMDSTATE state{};
    CheckHr(command->GetState(nullptr, TRUE, &state), "No-site state");
    Check(state == ECS_HIDDEN, "No site must not select an arbitrary folder");
    Check(FAILED(command->Invoke(nullptr, nullptr)), "Invocation without a target must fail");
    ComPtr<IShellItem> file, virtualFolder;
    CheckHr(SHCreateItemFromParsingName(dllPath, nullptr, IID_PPV_ARGS(&file)), "DLL shell item");
    CheckHr(SHGetKnownFolderItem(FOLDERID_ComputerFolder, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(&virtualFolder)), "Virtual shell item");
    for (auto* item : {file.Get(), virtualFolder.Get()})
    {
        ComPtr<IShellItemArray> array;
        CheckHr(SHCreateShellItemArrayFromShellItem(item, IID_PPV_ARGS(&array)), "Unsupported selection");
        CheckHr(command->GetState(array.Get(), TRUE, &state), "Unsupported selection state");
        Check(state == ECS_HIDDEN, "Files and virtual folders must be hidden");
        Check(FAILED(command->Invoke(array.Get(), nullptr)), "Unsupported invocation must fail");
    }
    PIDLIST_ABSOLUTE pidl = nullptr;
    CheckHr(SHGetIDListFromObject(directory, &pidl), "Directory PIDL");
    PCIDLIST_ABSOLUTE two[] = {pidl, pidl};
    ComPtr<IShellItemArray> multiple;
    const HRESULT arrayResult = SHCreateShellItemArrayFromIDLists(2, two, &multiple);
    CoTaskMemFree(pidl);
    CheckHr(arrayResult, "Multi-selection array");
    CheckHr(command->GetState(multiple.Get(), TRUE, &state), "Multi-selection state");
    Check(state == ECS_HIDDEN, "Multi-selection must not pick an arbitrary folder");
    Check(FAILED(command->Invoke(multiple.Get(), nullptr)), "Multi-selection invocation must fail");
    std::cout << "PASS no-site, file, virtual-folder and multi-selection rejection\n";

    if (!registered)
    {
        ComPtr<IObjectWithSite> withSite;
        CheckHr(command->QueryInterface(IID_PPV_ARGS(&withSite)), "IObjectWithSite");
        PIDLIST_ABSOLUTE physical = nullptr, desktop = nullptr;
        CheckHr(SHGetIDListFromObject(directory, &physical), "Physical background PIDL");
        const HRESULT desktopResult = SHGetSpecialFolderLocation(nullptr, CSIDL_DESKTOP, &desktop);
        if (FAILED(desktopResult)) { CoTaskMemFree(physical); CheckHr(desktopResult, "Desktop root PIDL"); }
        auto physicalSite = Microsoft::WRL::Make<TestFolderSite>();
        auto desktopSite = Microsoft::WRL::Make<TestFolderSite>();
        Check(physicalSite && desktopSite, "Allocate test sites");
        const HRESULT physicalResult = physicalSite->Initialize(physical);
        const HRESULT initializeDesktop = desktopSite->Initialize(desktop);
        CoTaskMemFree(physical); CoTaskMemFree(desktop);
        CheckHr(physicalResult, "Initialize physical site"); CheckHr(initializeDesktop, "Initialize desktop site");
        for (auto* site : {physicalSite.Get(), desktopSite.Get()})
        {
            CheckHr(withSite->SetSite(static_cast<IServiceProvider*>(site)), "Set background site");
            CheckHr(command->GetState(nullptr, TRUE, &state), "Background state");
            Check(state == ECS_ENABLED, "Filesystem and desktop backgrounds must be enabled");
        }
        CheckHr(withSite->SetSite(nullptr), "Clear site");
        std::cout << "PASS physical and desktop background resolution (simulated Explorer sites)\n";
    }
}
int wmain(int argc, wchar_t** argv)
{
    const bool registered = argc == 3 && std::wstring(argv[2]) == L"--registered";
    const bool targetMode = argc == 4 && (std::wstring(argv[2]) == L"--invoke" || std::wstring(argv[2]) == L"--target");
    if (argc != 2 && !registered && !targetMode) { std::cerr << "Usage: ShellSmoke <absolute-dll-path> [--registered | --target <directory> | --invoke <directory>]\n"; return 2; }
    if (FAILED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED))) { std::cerr << "COM initialization failed\n"; return 1; }
    try
    {
        CheckQuoting();
        auto module = LoadLibraryW(argv[1]); Check(module != nullptr, "DLL load failed");
        auto factoryFunction = reinterpret_cast<GetFactory>(GetProcAddress(module, "DllGetClassObject"));
        auto unload = reinterpret_cast<CanUnload>(GetProcAddress(module, "DllCanUnloadNow"));
        Check(factoryFunction && unload, "Missing COM exports");
        Check(unload() == S_OK, "Initially unloadable");
        {
            ComPtr<IClassFactory> factory;
            Check(SUCCEEDED(factoryFunction(CommandId, IID_PPV_ARGS(&factory))), "Class factory");
            Check(unload() == S_FALSE, "Factory pins module");
            ComPtr<IExplorerCommand> command;
            if (registered)
                CheckHr(CoCreateInstance(CommandId, nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&command)), "Packaged COM activation failed");
            else Check(SUCCEEDED(factory->CreateInstance(nullptr, IID_PPV_ARGS(&command))), "Create command");
            PWSTR title = nullptr;
            Check(SUCCEEDED(command->GetTitle(nullptr, &title)), "Title");
            Check(std::wstring(title) == L"Create New File", "Correct title"); CoTaskMemFree(title);
            PWSTR icon = nullptr;
            Check(SUCCEEDED(command->GetIcon(nullptr, &icon)) && icon, "Icon");
            const std::wstring iconPath(icon); CoTaskMemFree(icon);
            const std::wstring iconSuffix = L"\\Assets\\AppIcon.ico";
            Check(iconPath.size() > iconSuffix.size() && iconPath.compare(iconPath.size() - iconSuffix.size(), iconSuffix.size(), iconSuffix) == 0, "Icon path is package-relative");
            if (registered) Check(GetFileAttributesW(iconPath.c_str()) != INVALID_FILE_ATTRIBUTES, "Registered icon file exists");
            GUID canonical{}; Check(SUCCEEDED(command->GetCanonicalName(&canonical)) && canonical == CommandId, "Canonical command ID");
            EXPCMDSTATE state{}; Check(command->GetState(nullptr, FALSE, &state) == E_PENDING, "Fast state avoids blocking");
            EXPCMDFLAGS flags{}; Check(SUCCEEDED(command->GetFlags(&flags)) && flags == ECF_DEFAULT, "Flags");
            ComPtr<IShellItem> folder;
            PWSTR desktop = nullptr; CheckHr(SHGetKnownFolderPath(FOLDERID_Desktop, 0, nullptr, &desktop), "Desktop path");
            const wchar_t* path = argc == 4 ? argv[3] : desktop;
            Check(SUCCEEDED(SHCreateItemFromParsingName(path, nullptr, IID_PPV_ARGS(&folder))), "Resolve directory");
            CoTaskMemFree(desktop);
            ComPtr<IShellItemArray> selection;
            Check(SUCCEEDED(SHCreateShellItemArrayFromShellItem(folder.Get(), IID_PPV_ARGS(&selection))), "Selection array");
            Check(SUCCEEDED(command->GetState(selection.Get(), TRUE, &state)) && state == ECS_ENABLED, "Folder command enabled");
            CheckStateCases(command.Get(), folder.Get(), argv[1], registered);
            if (argc == 4 && std::wstring(argv[2]) == L"--invoke") Check(SUCCEEDED(command->Invoke(selection.Get(), nullptr)), "Launch app");
        }
        Check(unload() == S_OK, "All COM references released");
        FreeLibrary(module); CoUninitialize();
        std::cout << "PASS native COM factory, title, state, selection, lifetime" << std::endl;
        return 0;
    }
    catch (const char* message) { std::cerr << "FAIL " << message << std::endl; CoUninitialize(); return 1; }
}
