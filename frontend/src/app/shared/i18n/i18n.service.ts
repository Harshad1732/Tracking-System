import { Injectable, computed, signal } from '@angular/core';
import { LANGUAGES, LangCode, LanguageOption, TRANSLATIONS } from './translations';

const STORAGE_KEY = 'tracker.lang';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly _lang = signal<LangCode>(this.restore());
  readonly lang = this._lang.asReadonly();
  readonly languages: LanguageOption[] = LANGUAGES;
  readonly current = computed<LanguageOption>(() =>
    LANGUAGES.find(l => l.code === this._lang())!
  );

  setLang(code: LangCode): void {
    this._lang.set(code);
    try { localStorage.setItem(STORAGE_KEY, code); } catch { /* ignore */ }
    if (typeof document !== 'undefined') {
      document.documentElement.lang = code;
    }
  }

  t(key: string): string {
    const code = this._lang();
    const dict = TRANSLATIONS[code];
    return dict[key] ?? TRANSLATIONS.en[key] ?? key;
  }

  private restore(): LangCode {
    try {
      const stored = localStorage.getItem(STORAGE_KEY) as LangCode | null;
      if (stored && TRANSLATIONS[stored]) return stored;
    } catch { /* ignore */ }
    return 'en';
  }
}
