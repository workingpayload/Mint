#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <wrl.h>
#include <atomic>
#include <string>
#include <new>
#include "CommandLine.h"

using Microsoft::WRL::ComPtr;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;
using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::Make;

// This ID is part of the package's public registration contract.
static const CLSID CLSID_CreateFile = {0xc419d0d4,0x39ca,0x43a8,{0x9b,0x43,0x75,0x51,0x4b,0xab,0x6b,0x78}};
static HMODULE moduleHandle;
static std::atomic<long> objects{0};
static std::atomic<long> locks{0};
struct ObjectLifetime { ObjectLifetime() { ++objects; } ~ObjectLifetime() { --objects; } };

// Resolves a path relative to the folder containing this DLL (the package/install root).
static HRESULT SiblingPath(const wchar_t* relative, std::wstring& result)
{
    std::wstring module(32768, L'\0');
    DWORD length = GetModuleFileNameW(moduleHandle, module.data(), static_cast<DWORD>(module.size()));
    if (!length || length >= module.size()) return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
    module.resize(length);
    const auto slash = module.find_last_of(L"\\/");
    if (slash == std::wstring::npos) return E_FAIL;
    result = module.substr(0, slash + 1) + relative;
    return S_OK;
}

class ExplorerCommand final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
    ObjectLifetime lifetime;
    ComPtr<IUnknown> site;
    HRESULT Target(IShellItemArray* selection, IShellItem** result)
    {
        *result = nullptr;
        DWORD count = 0;
        if (selection)
        {
            HRESULT hr = selection->GetCount(&count); if (FAILED(hr)) return hr;
            if (count > 1) return E_INVALIDARG;
            if (count == 1) return selection->GetItemAt(0, result);
        }
        ComPtr<IServiceProvider> provider;
        if (!site || FAILED(site.As(&provider))) return E_FAIL;
        ComPtr<IFolderView> view;
        HRESULT hr = provider->QueryService(SID_SFolderView, IID_PPV_ARGS(&view));
        if (FAILED(hr))
        {
            ComPtr<IShellBrowser> browser;
            hr = provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&browser));
            if (FAILED(hr)) return hr;
            ComPtr<IShellView> shellView;
            hr = browser->QueryActiveShellView(&shellView); if (FAILED(hr)) return hr;
            hr = shellView.As(&view); if (FAILED(hr)) return hr;
        }
        ComPtr<IPersistFolder2> folder;
        hr = view->GetFolder(IID_PPV_ARGS(&folder)); if (FAILED(hr)) return hr;
        PIDLIST_ABSOLUTE pidl = nullptr;
        hr = folder->GetCurFolder(&pidl); if (FAILED(hr)) return hr;
        // Desktop is a virtual shell root. Resolve its actual per-user filesystem folder.
        hr = ILIsEmpty(pidl) ? SHGetKnownFolderItem(FOLDERID_Desktop, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(result))
                            : SHCreateItemFromIDList(pidl, IID_PPV_ARGS(result));
        CoTaskMemFree(pidl);
        return hr;
    }
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* title) override { return SHStrDupW(L"Create New File", title); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        *icon = nullptr;
        try
        {
            std::wstring path;
            HRESULT hr = SiblingPath(L"Assets\\AppIcon.ico", path); if (FAILED(hr)) return hr;
            return SHStrDupW(path.c_str(), icon);
        }
        catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
    }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* text) override { return SHStrDupW(L"Create an empty file or use a text template", text); }
    IFACEMETHODIMP GetCanonicalName(GUID* name) override { *name = CLSID_CreateFile; return S_OK; }
    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL slow, EXPCMDSTATE* state) override
    {
        *state = ECS_DISABLED;
        // Never resolve folders or touch the filesystem on Explorer's fast query path.
        if (!slow) return E_PENDING;
        ComPtr<IShellItem> item;
        if (FAILED(Target(selection, &item))) { *state = ECS_HIDDEN; return S_OK; }
        SFGAOF attributes = 0;
        if (SUCCEEDED(item->GetAttributes(SFGAO_FOLDER | SFGAO_FILESYSTEM, &attributes)) &&
            (attributes & (SFGAO_FOLDER | SFGAO_FILESYSTEM)) == (SFGAO_FOLDER | SFGAO_FILESYSTEM)) *state = ECS_ENABLED;
        else *state = ECS_HIDDEN;
        return S_OK;
    }
    IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
    {
        try
        {
            ComPtr<IShellItem> item;
            HRESULT hr = Target(selection, &item); if (FAILED(hr)) return hr;
            SFGAOF attributes = 0;
            hr = item->GetAttributes(SFGAO_FOLDER | SFGAO_FILESYSTEM, &attributes);
            if (FAILED(hr) || (attributes & (SFGAO_FOLDER | SFGAO_FILESYSTEM)) != (SFGAO_FOLDER | SFGAO_FILESYSTEM)) return E_INVALIDARG;
            PWSTR rawPath = nullptr;
            hr = item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath); if (FAILED(hr)) return hr;
            std::wstring folder;
            try { folder = rawPath; } catch (...) { CoTaskMemFree(rawPath); throw; }
            CoTaskMemFree(rawPath);
            std::wstring executable;
            hr = SiblingPath(L"CreateFile.App.exe", executable); if (FAILED(hr)) return hr;
            auto command = QuoteArgument(executable) + L" --folder " + QuoteArgument(folder);
            STARTUPINFOW startup{sizeof(startup)};
            PROCESS_INFORMATION process{};
            if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process))
                return HRESULT_FROM_WIN32(GetLastError());
            CloseHandle(process.hThread); CloseHandle(process.hProcess);
            return S_OK; // Do not wait for the UI or perform file creation inside Explorer.
        }
        catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
        catch (...) { return E_FAIL; }
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_DEFAULT; return S_OK; }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override { *commands = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP SetSite(IUnknown* output) override { site = output; return S_OK; }
    IFACEMETHODIMP GetSite(REFIID iid, void** output) override { if (!site) { *output = nullptr; return E_FAIL; } return site->QueryInterface(iid, output); }
};

class Factory final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IClassFactory>
{
    ObjectLifetime lifetime;
public:
    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** output) override
    {
        *output = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        auto command = Make<ExplorerCommand>();
        return command ? command->QueryInterface(iid, output) : E_OUTOFMEMORY;
    }
    IFACEMETHODIMP LockServer(BOOL lock) override { if (lock) ++locks; else --locks; return S_OK; }
};
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** output)
{
    *output = nullptr;
    if (clsid != CLSID_CreateFile) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = Make<Factory>();
    return factory ? factory->QueryInterface(iid, output) : E_OUTOFMEMORY;
}
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objects == 0 && locks == 0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID)
{
    // The static CRT needs thread attach/detach notifications; do not disable them.
    if (reason == DLL_PROCESS_ATTACH) moduleHandle = module;
    return TRUE;
}

