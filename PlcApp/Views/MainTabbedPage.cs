namespace PlcApp.Views;

/// <summary>
/// Containerul cu cele trei tab-uri. Paginile isi pastreaza designul propriu;
/// aici doar se aseaza una langa alta si primesc titlul afisat pe tab.
/// </summary>
public class MainTabbedPage : TabbedPage
{
    // ================= CULORILE BAREI DE TAB-URI =================
    // Acestea sunt singurele linii de schimbat daca vrei alta nuanta.
    // Prima valoare = tema luminoasa, a doua = tema intunecata.

    /// <summary>Fundalul barei. #0B1220 e culoarea din splash screen / icon.</summary>
    private static readonly Color BarLight = Color.FromArgb("#FFFFFF");
    private static readonly Color BarDark = Color.FromArgb("#0B1220");

    /// <summary>Textul tab-urilor neselectate.</summary>
    private static readonly Color TextLight = Color.FromArgb("#0F172A");
    private static readonly Color TextDark = Color.FromArgb("#E2E8F0");

    /// <summary>Tab-ul activ — aceeasi nuanta ca accentul Teal din aplicatie.</summary>
    private static readonly Color Accent = Color.FromArgb("#14B8A6");

    public MainTabbedPage(MainPage main, StatusPage status, ControlPage control)
    {
        Title = "Monitorizare PLC";

        // Titlul paginii principale e "Monitorizare PLC"; pe tab ar fi prea lung.
        main.Title = "Contoare";

        Children.Add(main);
        Children.Add(status);
        Children.Add(control);

        // SetAppThemeColor leaga valoarea de tema curenta, deci bara se schimba
        // singura cand apesi butonul "Tema" din pagina Contoare.
        this.SetAppThemeColor(BarBackgroundColorProperty, BarLight, BarDark);
        this.SetAppThemeColor(BarTextColorProperty, TextLight, TextDark);
        this.SetAppThemeColor(UnselectedTabColorProperty, BarLight, BarDark);

        SelectedTabColor = Accent;
    }
}