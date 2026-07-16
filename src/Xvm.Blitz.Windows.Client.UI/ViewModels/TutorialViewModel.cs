using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Settings;
using Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public sealed class TutorialViewModel : ReactiveObject
{
    private readonly AppSettings _settings;

    private readonly Action? _onFinished;

    private int _currentStepIndex;

    private double _contentOpacity = 1;

    private double _contentOffsetY;

    public ObservableCollection<TutorialStep> Steps { get; } = new(CreateSteps());

    public TutorialStep CurrentStep => Steps[CurrentStepIndex];

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentStepIndex, value);
            this.RaisePropertyChanged(nameof(CurrentStep));
            this.RaisePropertyChanged(nameof(StepProgressText));
            this.RaisePropertyChanged(nameof(ProgressRatio));
            this.RaisePropertyChanged(nameof(CanGoPrevious));
            this.RaisePropertyChanged(nameof(IsLastStep));
            this.RaisePropertyChanged(nameof(NextButtonText));
            this.RaisePropertyChanged(nameof(IsWelcomeVisible));
            this.RaisePropertyChanged(nameof(IsAuthorizationVisible));
            this.RaisePropertyChanged(nameof(IsLoadingScreenVisible));
            this.RaisePropertyChanged(nameof(IsReplaysVisible));
            this.RaisePropertyChanged(nameof(IsOverlaysVisible));
            this.RaisePropertyChanged(nameof(IsHotkeyVisible));
            this.RaisePropertyChanged(nameof(IsTrayVisible));
            this.RaisePropertyChanged(nameof(IsUpdatesVisible));
            this.RaisePropertyChanged(nameof(IsInterfaceVisible));
            this.RaisePropertyChanged(nameof(IsBattleFlowVisible));
            this.RaisePropertyChanged(nameof(IsFinishVisible));
        }
    }

    public double ContentOpacity
    {
        get => _contentOpacity;
        set => this.RaiseAndSetIfChanged(ref _contentOpacity, value);
    }

    public double ContentOffsetY
    {
        get => _contentOffsetY;
        set => this.RaiseAndSetIfChanged(ref _contentOffsetY, value);
    }

    public string StepProgressText => $"{CurrentStepIndex + 1} / {Steps.Count}";

    public double ProgressRatio => (CurrentStepIndex + 1) / (double)Steps.Count;

    public bool CanGoPrevious => CurrentStepIndex > 0;

    public bool IsLastStep => CurrentStepIndex >= Steps.Count - 1;

    public string NextButtonText => IsLastStep ? "Готово" : "Далее";

    public bool IsWelcomeVisible => CurrentStep.Illustration == TutorialIllustration.Welcome;

    public bool IsAuthorizationVisible => CurrentStep.Illustration == TutorialIllustration.Authorization;

    public bool IsLoadingScreenVisible => CurrentStep.Illustration == TutorialIllustration.LoadingScreen;

    public bool IsReplaysVisible => CurrentStep.Illustration == TutorialIllustration.Replays;

    public bool IsOverlaysVisible => CurrentStep.Illustration == TutorialIllustration.Overlays;

    public bool IsHotkeyVisible => CurrentStep.Illustration == TutorialIllustration.Hotkey;

    public bool IsTrayVisible => CurrentStep.Illustration == TutorialIllustration.Tray;

    public bool IsUpdatesVisible => CurrentStep.Illustration == TutorialIllustration.Updates;

    public bool IsInterfaceVisible => CurrentStep.Illustration == TutorialIllustration.Interface;

    public bool IsBattleFlowVisible => CurrentStep.Illustration == TutorialIllustration.BattleFlow;

    public bool IsFinishVisible => CurrentStep.Illustration == TutorialIllustration.Finish;

    public ReactiveCommand<Unit, Unit> NextCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }

    public ReactiveCommand<Unit, Unit> SkipCommand { get; }

    public TutorialViewModel(AppSettings settings, Action? onFinished = null)
    {
        _settings = settings;
        _onFinished = onFinished;

        var uiScheduler = RxApp.MainThreadScheduler;
        NextCommand = ReactiveCommand.CreateFromTask(GoNextAsync, outputScheduler: uiScheduler);
        PreviousCommand = ReactiveCommand.CreateFromTask(
            GoPreviousAsync,
            this.WhenAnyValue(viewModel => viewModel.CanGoPrevious),
            uiScheduler);
        SkipCommand = ReactiveCommand.Create(Finish, outputScheduler: uiScheduler);
    }

    private async Task GoNextAsync()
    {
        if (IsLastStep)
        {
            Finish();
            return;
        }

        await AnimateStepChangeAsync(CurrentStepIndex + 1);
    }

    private async Task GoPreviousAsync()
    {
        if (!CanGoPrevious)
            return;

        await AnimateStepChangeAsync(CurrentStepIndex - 1);
    }

    private async Task AnimateStepChangeAsync(int targetIndex)
    {
        ContentOpacity = 0;
        ContentOffsetY = 16;
        await Task.Delay(180);
        CurrentStepIndex = targetIndex;
        ContentOffsetY = -10;
        await Task.Delay(16);
        ContentOpacity = 1;
        ContentOffsetY = 0;
    }

    public void MarkAsSeen()
    {
        if (_settings.HasSeenTutorial)
            return;

        _settings.HasSeenTutorial = true;
        AppSettings.Save(_settings);
    }

    private void Finish()
    {
        MarkAsSeen();
        _onFinished?.Invoke();
    }

    private static IEnumerable<TutorialStep> CreateSteps()
    {
        yield return new TutorialStep
        {
            Title = "Добро пожаловать в XVM Blitz",
            Description =
                "Приложение показывает статистику союзников и противников во время боя в Tanks Blitz: ник, танк, бои и процент побед.",
            Tip = "Пройдите короткий тур — займёт меньше минуты.",
            Illustration = TutorialIllustration.Welcome,
            Highlights =
            [
                "Автоматическое распознавание боя",
                "Окна статистики поверх игры",
                "Гибкая настройка отображения"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Авторизация",
            Description =
                "Нажмите «Войти» в правом верхнем углу и введите API-ключ с сайта xvmblitz.ru. Без ключа статистика не загрузится.",
            Tip = "В профиле видно месячную квоту запросов и предупреждения при её исчерпании.",
            Illustration = TutorialIllustration.Authorization,
            Highlights =
            [
                "Кнопка «Войти» / «Профиль»",
                "Ввод и сохранение API-ключа",
                "Просмотр остатка квоты"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Экран загрузки боя",
            Description =
                "Для распознавания нужно заменить файлы экрана загрузки в папке игры. Пока замена не сделана, статистика не появится.",
            Tip = "Откройте «Заменить», укажите папку Tanks Blitz и нажмите «Заменить».",
            Illustration = TutorialIllustration.LoadingScreen,
            Highlights =
            [
                "Выбор папки с игрой",
                "Замена файлов XVM",
                "Возврат исходных файлов при необходимости"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Папка реплеев",
            Description =
                "Приложение следит за папкой сохранённых реплеев — появление нового файла означает начало боя.",
            Tip = "Обычно путь: Документы → TanksBlitz → replays. При необходимости укажите свой.",
            Illustration = TutorialIllustration.Replays,
            Highlights =
            [
                "Путь к сохранённым реплеям",
                "Кнопка выбора папки",
                "Быстрое открытие папки в проводнике"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Окна статистики",
            Description =
                "Во время боя появляются два окна: союзники и противники. В режиме настройки можно перетащить их мышью или задать координаты.",
            Tip = "Нажмите «Настроить отображение окон», расставьте окна и сохраните настройки.",
            Illustration = TutorialIllustration.Overlays,
            Highlights =
            [
                "Окно союзников и противников",
                "Перетаскивание в режиме настройки",
                "Точные координаты X/Y"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Горячая клавиша",
            Description =
                "Комбинация клавиш мгновенно скрывает или показывает окна статистики — удобно, если они мешают обзору.",
            Tip = "По умолчанию Ctrl + H. Кликните в поле и нажмите свою комбинацию.",
            Illustration = TutorialIllustration.Hotkey,
            Highlights =
            [
                "Глобальная горячая клавиша",
                "Работает даже поверх игры",
                "Любая удобная комбинация"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Сворачивание в трей",
            Description =
                "При закрытии основного окна приложение может оставаться в трее и продолжать отслеживать бои.",
            Tip = "Включите «Сворачивать в трей…», чтобы не завершать работу случайно.",
            Illustration = TutorialIllustration.Tray,
            Highlights =
            [
                "Работа в фоне",
                "Пункт «Показать» в меню трея",
                "Полный выход через «Выход»"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Обновления",
            Description =
                "В блоке «Обновление» видна текущая версия. Приложение само проверяет новые релизы примерно каждые 10 минут.",
            Tip = "Если доступна новая версия — нажмите «Скачать».",
            Illustration = TutorialIllustration.Updates,
            Highlights =
            [
                "Текущая и последняя версия",
                "Ручная проверка обновлений",
                "Ссылка на скачивание"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Интерфейс",
            Description =
                "Ползунок размера шрифта меняет масштаб текста во всём приложении — удобно для разных мониторов.",
            Tip = "Подберите комфортный размер и нажмите «Сохранить».",
            Illustration = TutorialIllustration.Interface,
            Highlights =
            [
                "Глобальный размер шрифта",
                "Мгновенный предпросмотр",
                "Сохранение вместе с остальными настройками"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Как это работает в бою",
            Description =
                "После настройки всё происходит автоматически: новый реплей → снимок экрана загрузки → запрос к API → окна со статистикой.",
            Tip = "Достаточно войти в бой — окна появятся сами, если экран загрузки заменён и ключ указан.",
            Illustration = TutorialIllustration.BattleFlow,
            Highlights =
            [
                "Обнаружение начала боя",
                "Распознавание игроков",
                "Отображение статистики поверх игры"
            ]
        };

        yield return new TutorialStep
        {
            Title = "Готово к работе",
            Description =
                "Краткий чеклист: войти по API-ключу, заменить экран загрузки, проверить путь к реплеям, расставить окна и сохранить настройки.",
            Tip = "Открыть обучение снова можно кнопкой «Обучение» в главном окне.",
            Illustration = TutorialIllustration.Finish,
            Highlights =
            [
                "Войти → Заменить экран → Реплеи",
                "Настроить окна и горячую клавишу",
                "Сохранить и играть"
            ]
        };
    }
}
