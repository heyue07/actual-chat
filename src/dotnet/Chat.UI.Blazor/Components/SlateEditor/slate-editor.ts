import React from "react"
import ReactDOM from "react-dom/client"
import { createSlateEditorCore } from './slate-editor-core';
import { SlateEditorHandle } from './slate-editor-handle';
import './slate-editor.css';

export class SlateEditor {
    private readonly blazorRef: DotNet.DotNetObject;
    private readonly editorDiv: HTMLDivElement;
    private readonly editorHandle: SlateEditorHandle;
    private readonly reactDomRoot: any;
    private readonly debug: boolean;

    static create(editorDiv: HTMLDivElement, blazorRef: DotNet.DotNetObject, debug : boolean): SlateEditor {
        return new SlateEditor(editorDiv, blazorRef, debug);
    }

    constructor(editorDiv: HTMLDivElement, blazorRef: DotNet.DotNetObject, debug : boolean) {
        this.editorDiv = editorDiv;
        this.blazorRef = blazorRef;
        this.debug = debug;
        this.editorHandle = new SlateEditorHandle();
        this.editorHandle.onPost = this.onPost;
        this.editorHandle.onMentionCommand = this.onMentionCommand;

        // @ts-ignore
        this.editorDiv.editorHandle = this.editorHandle;

        const slateEditor = () => createSlateEditorCore(this.editorHandle, this.debug)
        this.reactDomRoot = ReactDOM.createRoot(this.editorDiv);
        this.reactDomRoot.render(React.createElement(slateEditor));
        if (debug) console.log("SlateEditor.constructor");
    }

    public getText = () =>
        this.editorHandle.getText();

    private onPost = () =>
        this.blazorRef.invokeMethodAsync("Post", this.getText());

    public clearText = () =>
        this.editorHandle.clearText();

    private onMentionCommand = (cmd : string, args : string) : any =>
        this.blazorRef.invokeMethodAsync("MentionCommand", cmd, args);

    public insertMention = (mention : any) =>
        this.editorHandle.insertMention(mention);

    public setPlaceholder = (placeholder: string) =>
        this.editorHandle.setPlaceholder(placeholder);

    public focus = () => {
        const input = this.editorDiv.querySelector('div');
        input.focus();
        if (this.debug) console.log('focus');
    }

    private dispose() {
        this.reactDomRoot.unmount();
    }
}


