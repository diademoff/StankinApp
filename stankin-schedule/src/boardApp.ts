import { ApiClient } from './ApiClient';

const SITE_KEY = 'ysc1_gEQosFoIPUl2qcUUE83Ch2rLLRDpZhVA9VeieRTTe05d3a9b';
const PAGE_SIZE = 20;

// >>N → кликабельный якорь на пост (со скроллом и подсветкой через :target)
function formatPostText(text: string): string {
  const escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  return escaped.replace(/&gt;&gt;(\d+)/g, '<a href="#post-$1" class="text-blue-600 hover:underline">>>$1</a>');
}

export function boardApp(api: ApiClient) {
  return {
    view: 'list' as 'list' | 'thread',
    threads: [] as any[],
    thread: null as any,
    page: 1,
    hasMore: true,
    loading: false,
    error: '' as string,
    postText: '',
    sage: false,
    replyParentId: null as number | null,
    captchaWidgetId: null as number | null,
    depths: {} as Record<number, number>,

    get replyLabel(): string {
      if (this.view === 'thread')
        return this.replyParentId ? `Ответ на пост #${this.replyParentId}` : 'Ответ в тред';
      return 'Новый тред';
    },

    depthOf(post: any): number {
      if (this.depths[post.id] != null) return this.depths[post.id];
      const d = post.parentId != null
        ? this.depthOf((this.thread?.posts ?? []).find((p: any) => p.id === post.parentId) ?? {}) + 1
        : 0;
      this.depths[post.id] = d;
      return d;
    },

    init() {
      this.loadThreads(true);
      this.initCaptcha();
    },

    async loadThreads(reset = false) {
      if (reset) {
        this.page = 1;
        this.threads = [];
        this.hasMore = true;
      }
      this.loading = true;
      this.error = '';
      try {
        const items = await api.getThreads(this.page);
        this.threads = this.threads.concat(items);
        if (items.length < PAGE_SIZE) this.hasMore = false;
      } catch (e) {
        this.error = (e as Error).message;
      } finally {
        this.loading = false;
      }
    },

    loadMore() {
      this.page += 1;
      this.loadThreads();
    },

    async openThread(threadId: number) {
      this.loading = true;
      this.error = '';
      try {
        this.thread = await api.getThread(threadId);
        this.view = 'thread';
        this.replyParentId = null;
        this.postText = '';
        this.sage = false;
        this.depths = {};
      } catch (e) {
        this.error = (e as Error).message;
      } finally {
        this.loading = false;
      }
    },

    backToList() {
      this.view = 'list';
      this.thread = null;
      this.replyParentId = null;
      this.postText = '';
      this.loadThreads(true);
    },

    formatText(text: string): string {
      return formatPostText(text);
    },

    setReplyParent(postId: number) {
      this.replyParentId = this.replyParentId === postId ? null : postId;
    },

    async report(postId: number) {
      if (!confirm('Пожаловаться на этот пост?')) return;
      try {
        await api.reportPost(postId);
        alert('Жалоба отправлена');
      } catch (e) {
        alert((e as Error).message);
      }
    },

    initCaptcha() {
      const render = () => {
        const sc = (window as any).smartCaptcha;
        const el = this.$refs.captcha as HTMLElement | undefined;
        if (!sc?.render || !el) {
          setTimeout(render, 200);
          return;
        }
        this.captchaWidgetId = sc.render(el, { sitekey: SITE_KEY });
      };
      render();
    },

    getCaptchaToken(): string {
      const sc = (window as any).smartCaptcha;
      return sc?.getResponse?.(this.captchaWidgetId) ?? '';
    },

    resetCaptcha() {
      const sc = (window as any).smartCaptcha;
      if (sc?.reset && this.captchaWidgetId != null) sc.reset(this.captchaWidgetId);
    },

    async submit() {
      const text = this.postText.trim();
      if (!text) return;
      const token = this.getCaptchaToken();
      if (!token) {
        this.error = 'Пройдите капчу';
        return;
      }
      this.loading = true;
      this.error = '';
      try {
        if (this.view === 'thread') {
          await api.createReply(this.thread.id, text, token, this.replyParentId, this.sage);
        } else {
          const post = await api.createThread(text, token);
          await this.openThread(post.id);
        }
        this.postText = '';
        this.sage = false;
      } catch (e) {
        this.error = (e as Error).message;
      } finally {
        this.resetCaptcha();
        this.loading = false;
      }
    },
  };
}
