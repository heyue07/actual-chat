import {
    Subject,
    takeUntil,
    debounceTime,
    tap,
} from 'rxjs';
import { preventDefaultForEvent } from 'event-handling';
import { throttle } from 'promises';
import { Log } from 'logging';
import { MarkupEditor } from '../MarkupEditor/markup-editor';
import { ScreenSize } from '../../../UI.Blazor/Services/ScreenSize/screen-size';
import { LocalSettings } from '../../../UI.Blazor/Services/Settings/local-settings';
import { AttachmentList } from './attachment-list';

const { debugLog } = Log.get('MessageEditor');

export type PanelMode = 'Normal' | 'Narrow';

export class ChatMessageEditor {
    private readonly backupRequired$ = new Subject<void>();
    private readonly disposed$: Subject<void> = new Subject<void>();
    private blazorRef: DotNet.DotNetObject;
    private readonly editorDiv: HTMLDivElement;
    private markupEditor: MarkupEditor;
    private readonly input: HTMLDivElement;
    private readonly postButton: HTMLButtonElement;
    private readonly attachButton: HTMLButtonElement;
    private attachmentList: AttachmentList;
    private attachmentListElement: HTMLDivElement;
    private readonly attachmentListObserver: MutationObserver;
    private readonly notifyPanel: HTMLDivElement;
    private readonly notifyPanelObserver: MutationObserver;
    private lastHeight: number;
    private lastWidth: number;
    private isNarrowScreen: boolean = null; // Intended: updateLayout needs this on the first run
    private panelModel: PanelMode = null; // Intended: updateLayout needs this on the first run
    private hasContent: boolean = null; // Intended: updateHasContent needs this on the first run
    private isNotifyPanelOpen: boolean = false;
    private chatId: string;

    static create(editorDiv: HTMLDivElement, blazorRef: DotNet.DotNetObject): ChatMessageEditor {
        return new ChatMessageEditor(editorDiv, blazorRef);
    }

    constructor(editorDiv: HTMLDivElement, blazorRef: DotNet.DotNetObject) {
        this.editorDiv = editorDiv;
        this.blazorRef = blazorRef;
        this.input = this.editorDiv.querySelector(':scope .post-panel .message-input');
        this.postButton = this.editorDiv.querySelector(':scope .post-panel .post-message');
        this.attachButton = this.editorDiv.querySelector(':scope .attach-btn');
        this.notifyPanel = this.editorDiv.querySelector(':scope .notify-call-panel');

        this.updateLayout();
        this.updateHasContent();

        // Wiring up event listeners
        ScreenSize.event$
            .pipe(takeUntil(this.disposed$))
            .subscribe(this.updateLayoutThrottled);
        this.input.addEventListener('paste', this.onInputPaste);
        this.attachButton.addEventListener('click', this.onAttachButtonClick);
        this.notifyPanel.addEventListener('click', this.onNotifyPanelClick);
        this.backupRequired$.pipe(debounceTime(1000), tap(() => this.saveDraft())).subscribe();

        this.attachmentListObserver = new MutationObserver(this.updateAttachmentListState);
        this.attachmentListObserver.observe(this.editorDiv, {
            attributes: true,
            childList: true,
        });

        this.notifyPanelObserver = new MutationObserver(this.updateNotifyPanelRelated);
        this.notifyPanelObserver.observe(this.notifyPanel, {
            attributes: true,
        });
    }

    public dispose() {
        if (this.disposed$.closed)
            return;

        this.backupRequired$.complete();
        this.disposed$.next();
        this.disposed$.complete();
        this.input.removeEventListener('paste', this.onInputPaste);
        this.attachButton.removeEventListener('click', this.onAttachButtonClick);
        this.notifyPanel.removeEventListener('click', this.onNotifyPanelClick);
        if (this.attachmentListElement != null) {
            this.attachmentListElement.removeEventListener('wheel', this.onHorizontalScroll);
        }
        this.attachmentListObserver.disconnect();
        this.notifyPanelObserver.disconnect();
    }

    // Public methods

    private updateAttachmentListState = (mutationList, observer) => {
        mutationList.forEach(m => {
            m.addedNodes.forEach(element => {
                if (element.className == 'attachment-list-wrapper') {
                    if (!this.editorDiv.classList.contains('attachment-mode')) {
                        this.editorDiv.classList.add('attachment-mode');
                    }
                    this.attachmentListElement = this.editorDiv.querySelector('.attachment-list')
                    this.attachmentListElement.addEventListener('wheel', this.onHorizontalScroll);
                }
            });
            m.removedNodes.forEach(element => {
                if (element.className == 'attachment-list-wrapper') {
                    this.editorDiv.classList.remove('attachment-mode');
                    if (this.attachmentListElement != null) {
                        this.attachmentListElement.removeEventListener('wheel', this.onHorizontalScroll);
                    }
                }
            });
        })
    };

    private onHorizontalScroll = ((event: WheelEvent & { target: Element; }) => {
        preventDefaultForEvent(event);
        this.attachmentListElement.scrollBy({ left: event.deltaY < 0 ? -30 : 30, });
    });

    /** Called by Blazor */
    public onNestedControlsReady(markupEditor: MarkupEditor, attachmentList: AttachmentList)
    {
        this.markupEditor = markupEditor;
        this.markupEditor.changed = () => {
            this.backupRequired$.next();
            this.updateHasContent();
        }
        this.attachmentList = attachmentList;
        this.attachmentList.changed = () => {
            this.updateHasContent();
        }
        this.updateHasContent();
        if (this.isNarrowScreen)
            this.markupEditor.contentDiv.blur(); // We want to see the placeholder on mobile when you open a chat
    }

    /** Called by Blazor */
    public setChatId(chatId: string) {
        this.chatId = chatId;
        this.attachmentList.setChatId(chatId)
        this.restoreDraft();
    }

    // Event handlers

    private onAttachButtonClick = ((event: Event & { target: Element; }) => {
        this.attachmentList.showFilePicker();
        if (this.panelModel == 'Narrow') {
            this.markupEditor.focus();
            this.updateHasContent();
        }
    });

    private onReturnFocusOnInput = ((event: Event & { target: Element; }) => {
        if (this.panelModel == 'Narrow') {
            debugLog?.log("onReturnFocusOnInput");
            this.markupEditor.focus();
            this.updateHasContent();
        }
    });

    private onNotifyPanelClick = (async (event: Event & { target: Element; }) => {
        if (event.target == this.notifyPanel || event.target.classList.contains('notify-call-content')) {
            if (this.notifyPanel.classList.contains('panel-opening')) {
                await this.blazorRef.invokeMethodAsync('CloseNotifyPanel');
            }
        }
    });

    private onInputPaste = async (event: ClipboardEvent & { target: Element; }) => {
        // Get pasted data via clipboard API
        // We need to handle only files pasting.
        // Text pasting is controlled by markup editor.
        const clipboardData = event.clipboardData;
        let isAdding = false;
        for (const item of clipboardData.items) {
            if (item.kind === 'file') {
                if (!isAdding)
                    preventDefaultForEvent(event); // We can do it only in the sync part of async handler
                isAdding = true;
                const file = item.getAsFile();
                if (await this.attachmentList.add(this.chatId, file))
                    this.updateHasContent();
            }
        }
    };

    // Private methods

    private updateLayoutThrottled = throttle(() => this.updateLayout(), 250, 'delayHead');
    private updateLayout = () => {
        const width = window.visualViewport.width;
        const height = window.visualViewport.height;
        const isNarrowScreen = width < 1024;

        if (this.isNarrowScreen === isNarrowScreen) {
            if (!isNarrowScreen)
                return; // Nothing to update in desktop mode

            if (width != this.lastWidth) {
                // Orientation changed
                this.lastWidth = width;
                this.lastHeight = height;
                return;
            }
            if (height == this.lastHeight)
                return;

            // Maybe mobile keyboard pull-out / pull-in
            const minHeight = Math.min(height, this.lastHeight);
            const maxHeight = Math.max(height, this.lastHeight);
            const keyboardHeight = maxHeight - minHeight;
            debugLog?.log(`updateLayout: keyboardHeight:`, keyboardHeight, '/', maxHeight);
            if (keyboardHeight >= 0.2 * maxHeight) {
                // Mobile keyboard pull-out / pull-in
                const panelMode = Math.abs(height - minHeight) < 0.01 // FP: height == minHeight
                    ? 'Narrow'
                    : 'Normal';
                if (this.panelModel !== panelMode) {
                    this.panelModel = panelMode;
                    if (panelMode == 'Narrow')
                        this.editorDiv.classList.add('narrow-panel');
                    else
                        this.editorDiv.classList.remove('narrow-panel');
                }
            }
            this.lastHeight = height;
            return;
        }

        this.isNarrowScreen = isNarrowScreen;
        this.lastHeight = height;
        this.lastWidth = width;
        const buttons = this.editorDiv.querySelectorAll(':scope div.chat-audio-panel .btn');
        if (isNarrowScreen)
            buttons.forEach(b => b.addEventListener('click', this.onReturnFocusOnInput));
        else
            buttons.forEach(b => b.removeEventListener('click', this.onReturnFocusOnInput));
    }

    private updateHasContent() {
        const text = this.markupEditor?.getText() ?? '';
        const isTextMode = text != '' || this.attachmentList?.some();
        if (this.hasContent === isTextMode)
            return;

        this.hasContent = isTextMode;
        if (isTextMode)
            this.editorDiv.classList.add('text-mode');
        else
            this.editorDiv.classList.remove('text-mode');
        this.endAnimations();
    }

    private updateNotifyPanelRelated = () => {
        const isNotifyPanelOpen = this.notifyPanel.classList.contains('panel-opening');
        if (this.isNotifyPanelOpen === isNotifyPanelOpen)
            return;

        this.isNotifyPanelOpen = isNotifyPanelOpen;
        const attach = this.editorDiv.querySelector(':scope .attach-dropdown');
        const label = this.editorDiv.querySelector(':scope label');
        if (isNotifyPanelOpen) {
            self.setTimeout(() => {
                attach.classList.add('hidden');
                label.classList.add('hidden');
                this.markupEditor.isEditable(false);
            }, 150);
        } else {
            attach.classList.remove('hidden');
            label.classList.remove('hidden');
            this.markupEditor.isEditable(true);
        }

        if (this.notifyPanel.classList.contains('panel-closing')) {
            self.setTimeout(() => {
                this.notifyPanel.classList.replace('panel-closing', 'panel-closed');
            }, 150);
        }
    };

    private endAnimations(): void {
        this.notifyPanel.classList.remove('ha-opening', 'panel-closing');
        const playbackWrapper = this.editorDiv.querySelector('.playback-wrapper');
        if (!playbackWrapper)
            return;
        playbackWrapper.classList.replace('listen-on-to-off', 'listen-off');
        playbackWrapper.classList.replace('listen-off-to-on', 'listen-on');
    }

    private saveDraft() {
        if (!this.chatId)
            return;

        const text = this.markupEditor.getHtml();
        const data = {
            [`MessageDraft.${this.chatId}.Html`]: !!text ? text : null,
        };
        LocalSettings.setMany(data);
    }

    private restoreDraft() {
        const [html] = this.chatId && LocalSettings.getMany([`MessageDraft.${this.chatId}.Html`]);
        this.markupEditor.setHtml(html ?? "", ScreenSize.isWide());
    }
}
