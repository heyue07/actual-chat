import { preventDefaultForEvent } from 'event-handling';
import { fromEvent, Subject, takeUntil, debounceTime } from 'rxjs';

import { Log } from 'logging';
import { setTimeout } from 'timerQueue';
import { ScreenSize } from '../../Services/ScreenSize/screen-size';

const { debugLog } = Log.get('VisualMediaViewer');

class MoveState {
    imageRect: DOMRect;
    viewerRect: DOMRect;
    distance: number;

    constructor(
        imageRect = null,
        viewerRect = null,
        distance = 0) {
        this.imageRect = imageRect;
        this.viewerRect = viewerRect;
        this.distance = distance;
    }
}

export class VisualMediaViewer {
    private readonly disposed$: Subject<void> = new Subject<void>();
    private readonly overlay: HTMLElement;
    private readonly media: HTMLElement;
    private readonly header: HTMLElement;
    private readonly footer: HTMLElement;
    private readonly multiplier: number = 1.25;
    private startY: number = 0;
    private startX: number = 0;
    private deltaY: number = 0;
    private deltaX: number = 0;
    private prevY: number = 0;
    private prevX: number = 0;
    private originMediaWidth: number = 0;
    private originMediaHeight: number = 0;
    private isMovementStarted: boolean = false;
    private startDistance: number = 0;
    private startImageRect: DOMRect;
    private startImageTop: number = 0;
    private startImageLeft: number = 0;
    private headerBottom: number = 0;
    private footerTop: number = 0;
    private isFooterAndHeaderShown: boolean = false;
    private points: PointerEvent[] = new Array<PointerEvent>();
    private minWidth: number = 0;
    private minHeight: number = 0;
    private readonly maxWidth: number = 0;
    private readonly maxHeight: number = 0;

    private curState: MoveState = new MoveState();
    private prevState: MoveState = new MoveState();

    static create(imageViewer: HTMLElement, blazorRef: DotNet.DotNetObject): VisualMediaViewer {
        return new VisualMediaViewer(imageViewer, blazorRef);
    }

    constructor(
        private readonly imageViewer: HTMLElement,
        private readonly blazorRef: DotNet.DotNetObject
    ) {
        this.onScreenSizeChange();
        ScreenSize.event$
            .pipe(takeUntil(this.disposed$))
            .subscribe(() => this.onScreenSizeChange());
        let image = imageViewer.querySelector('img');
        let video = imageViewer.querySelector('video');
        if (image != null) {
            this.media = image;
            this.originMediaWidth = this.minWidth = this.round(this.media.getBoundingClientRect().width);
            this.originMediaHeight = this.minHeight = this.round(this.media.getBoundingClientRect().height);
        } else if (video != null) {
            this.media = video;
            this.media.oncanplay = (event) => {
                this.originMediaWidth = this.minWidth = this.round(this.media.getBoundingClientRect().width);
                this.originMediaHeight = this.minHeight = this.round(this.media.getBoundingClientRect().height);
            }
        } else {
            return;
        }

        this.overlay = this.imageViewer.closest('.modal-overlay');
        this.header = this.overlay.querySelector('.image-viewer-header');
        this.headerBottom = this.round(this.header.getBoundingClientRect().bottom);
        this.footer = this.overlay.querySelector('.image-viewer-footer');
        this.footerTop = this.round(this.footer.getBoundingClientRect().top);
        this.toggleFooterHeaderVisibility();
        this.maxHeight = window.innerHeight * 3;
        this.maxWidth = window.innerWidth * 3;

        fromEvent(this.overlay, 'wheel')
            .pipe(takeUntil(this.disposed$))
            .subscribe((event: WheelEvent) => this.onWheel(event));

        fromEvent(window, 'pointerdown', { capture: false })
            .pipe(takeUntil(this.disposed$))
            .subscribe((event: PointerEvent) => this.onPointerDown(event));

        fromEvent(window, 'pointerup')
            .pipe(takeUntil(this.disposed$))
            .subscribe((event: PointerEvent) => this.onPointerUp(event));

        fromEvent(window, 'pointercancel')
            .pipe(takeUntil(this.disposed$))
            .subscribe((event: PointerEvent) => this.onPointerUp(event));

        let vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    }

    public dispose() {
        if (this.disposed$.closed)
            return;

        this.disposed$.next();
        this.disposed$.complete();
    }

    private round = (value: number) : number => {
        return Math.round(value);
    }

    private logInfo(text: string) {
        return this.blazorRef.invokeMethodAsync('LogJS', text);
    }

    private onScreenSizeChange() {
        let vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    }

    private toggleFooterHeaderVisibility() {
        this.isFooterAndHeaderShown = !this.isFooterAndHeaderShown;
        if (this.isFooterAndHeaderShown) {
            this.footer.style.display = 'flex';
            this.header.style.display = 'flex';
        } else {
            this.footer.style.display = 'none';
            this.header.style.display = 'none';
        }
    }

    private centerImageX = (oldViewerRect: DOMRect) => {
        const viewerRect = this.imageViewer.getBoundingClientRect();
        const viewerLeft = this.round(viewerRect.left);
        const viewerWidth = this.round(viewerRect.width);
        const windowWidth = window.innerWidth;
        let left = viewerLeft;

        if (viewerWidth < windowWidth
            || viewerWidth == windowWidth
            || oldViewerRect.width == windowWidth) {
            left = this.round((windowWidth - viewerWidth) / 2);
        } else {
            let oldLeftOffsetPercentage = oldViewerRect.left / (oldViewerRect.width - windowWidth);
            let viewerXOffset = viewerWidth - windowWidth;
            left = this.round(viewerXOffset * oldLeftOffsetPercentage);
        }
        this.imageViewer.style.left = left + 'px';
    }

    private centerImageY = (oldViewerRect: DOMRect) => {
        const viewerRect = this.imageViewer.getBoundingClientRect();
        const viewerTop = this.round(viewerRect.top);
        const viewerHeight = this.round(viewerRect.height);
        const windowHeight = window.innerHeight;
        let top = viewerTop;

        if (viewerHeight < windowHeight
            || (viewerHeight / windowHeight <= 1.03 && viewerHeight / windowHeight >= 0.97)
            || (oldViewerRect.height / windowHeight <= 1.03 && oldViewerRect.height / windowHeight >= 0.97)) {
            top = this.round((windowHeight - viewerHeight) / 2);
        } else {
            let oldTopOffsetPercentage = oldViewerRect.top / (oldViewerRect.height - windowHeight);
            let viewerYOffset = viewerHeight - windowHeight;
            top = this.round(viewerYOffset * oldTopOffsetPercentage);
        }
        this.imageViewer.style.top = top + 'px';
    }

    private getImageAndMouseX = (event: MouseEvent, rect: DOMRect) => {
        this.startX = event.clientX;
        this.startImageLeft = this.round(rect.left);
        this.deltaX = this.startX - this.startImageLeft;
    }

    private getImageAndMouseY = (event: MouseEvent, rect: DOMRect) => {
        this.startY = event.clientY;
        this.startImageTop = this.round(rect.top);
        this.deltaY = this.startY - this.startImageTop;
    }

    private getLeft = (event: MouseEvent, rect: DOMRect) : number => {
        let x = event.pageX;
        let roundLeft = this.round(rect.left);
        let roundRight = this.round(rect.right);
        let left = roundLeft;
        if (roundLeft >= 0 && roundRight <= window.innerWidth
            || roundLeft == 0 && rect.width > window.innerWidth && event.movementX >= 0
            || roundRight == window.innerWidth && rect.width > window.innerWidth && event.movementX <= 0) {
            left = roundLeft;
            this.getImageAndMouseX(event, rect);
        } else if (roundLeft > 0 && event.movementX > 0) {
            left = 0;
        } else if ((window.innerWidth - roundRight > 0) && event.movementX < 0) {
            left = window.innerWidth - rect.width;
        }
        else {
            left = x - this.deltaX;
        }
        return left;
    }

    private getTop = (event: MouseEvent, rect: DOMRect) : number => {
        let y = event.pageY;
        let roundTop = this.round(rect.top);
        let roundBottom = this.round(rect.bottom);
        let top = roundTop;
        if (roundTop >= 0 && roundBottom <= window.innerHeight
            || roundTop == 0 && rect.height > window.innerHeight && event.movementY >= 0
            || roundBottom <= window.innerHeight && rect.height > window.innerHeight && event.movementY <= 0) {
            top = roundTop;
            this.getImageAndMouseY(event, rect);
        } else if (roundTop > 0 && event.movementY >= 0) {
            top = 0;
        } else if (roundBottom < window.innerHeight && event.movementY <= 0) {
            top = window.innerHeight - rect.height;
        } else {
            top = y - this.deltaY;
        }
        return top;
    }

    private getDistance() : number {
        let x = Math.abs(this.points[0].clientX - this.points[1].clientX);
        let y = Math.abs(this.points[0].clientY - this.points[1].clientY);
        return Math.sqrt(x*x + y*y);
    }

    private removeEvent(event: PointerEvent) {
        const index = this.points.findIndex(
            (e) => e.pointerId === event.pointerId
        );
        this.points.splice(index, 1);
    }

    // Event handlers

    private onWheel = (event: WheelEvent) => {
        const viewerRect = this.imageViewer.getBoundingClientRect();
        const delta = event.deltaY;
        const windowWidth = window.innerWidth;
        const windowHeight = window.innerHeight;
        const width = this.media.getBoundingClientRect().width;
        const height = this.media.getBoundingClientRect().height;
        let newWidth = width;
        preventDefaultForEvent(event);
        if (delta < 0) {
            // up
            newWidth = width * this.multiplier;
            if (newWidth / windowWidth <= 1.03 && newWidth / windowWidth >= 0.97) {
                newWidth = windowWidth;
            }
            let newMaxWidth = newWidth;
            let newMaxHeight = height * this.multiplier;
            if ((newMaxHeight > windowHeight * 3 || newMaxWidth > windowWidth * 3)) {
                return;
            } else {
                this.media.style.width = this.imageViewer.style.width = this.round(newWidth) + 'px';
                this.media.style.maxHeight = this.round(newMaxHeight) + 'px';
                this.media.style.maxWidth = this.round(newMaxWidth) + 'px';
                this.centerImageX(viewerRect);
                this.centerImageY(viewerRect);
            }
        } else {
            // down
            newWidth = width / this.multiplier;
            if (newWidth / windowWidth < 1.03 && newWidth / windowWidth > 0.97) {
                newWidth = windowWidth;
            }
            let newMaxWidth = newWidth;
            let newMaxHeight = height / this.multiplier;
            if (newWidth < 100 && newMaxHeight < 100) {
                return;
            } else {
                this.media.style.width = this.imageViewer.style.width = this.round(newWidth) + 'px';
                this.media.style.maxHeight = this.round(newMaxHeight) + 'px';
                this.media.style.maxWidth = this.round(newMaxWidth) + 'px';
                this.centerImageX(viewerRect);
                this.centerImageY(viewerRect);
            }
        }
    };

    private onPointerDown = (event: PointerEvent) => {
        switch (event.pointerType) {
            case 'mouse':
                this.points.push(event);
                let target = event.target as HTMLElement;
                if (this.isRequiredClass(target)) {
                    const parent = this.imageViewer.parentElement;
                    this.headerBottom = this.round(parent.querySelector('.image-viewer-header')
                                                       .getBoundingClientRect().bottom);
                    this.footerTop = this.round(parent.querySelector('.image-viewer-footer')
                                                    .getBoundingClientRect().top);
                    const viewerRect = this.imageViewer.getBoundingClientRect();
                    const viewerTop = this.round(viewerRect.top);
                    const viewerBottom = this.round(viewerRect.bottom);
                    const viewerLeft = this.round(viewerRect.left);
                    const viewerRight = this.round(viewerRect.right);
                    if (viewerTop < 0 || viewerBottom > window.innerHeight || viewerLeft < 0 || viewerRight > window.innerWidth) {
                        this.getImageAndMouseX(event, viewerRect);
                        this.getImageAndMouseY(event, viewerRect);
                        window.addEventListener('pointermove', this.onPointerMove);
                    } else {
                        preventDefaultForEvent(event);
                    }
                }
                break;
            case 'touch':
                this.media.style.touchAction = 'none';
                this.imageViewer.style.touchAction = 'none';
                this.overlay.style.touchAction = 'none';
                this.points.push(event);
                this.startImageRect = this.media.getBoundingClientRect();
                let imageRect = this.media.getBoundingClientRect();
                let viewerRect = this.imageViewer.getBoundingClientRect();
                this.curState = new MoveState(imageRect, viewerRect, 0);
                this.prevState = new MoveState(imageRect, viewerRect, 0);
                window.addEventListener('pointermove', this.onTouchableMove);

                if (this.points.length === 2) {
                    this.startDistance = this.getDistance();
                } else if (this.points.length === 1) {
                    this.startY = event.pageY;
                    this.startX = event.pageX;
                    this.prevY = this.startY;
                    this.prevX = this.startX;
                }
                break;
            default:
                break;
        }
    };

    private isRequiredClass(target: HTMLElement) : boolean {
        return target.classList.contains('image-viewer-content')
            || target.classList.contains('image-container')
            || target.classList.contains('video-container');
    }

    private onPointerMove = (event: PointerEvent) => {
        preventDefaultForEvent(event);
        let rect = this.imageViewer.getBoundingClientRect();
        let topPosition = this.getTop(event, rect);
        let leftPosition = this.getLeft(event, rect);
        this.imageViewer.style.top = topPosition + 'px';
        this.imageViewer.style.left = leftPosition + 'px';
    };

    private onPointerUp = (event: PointerEvent) => {
        window.removeEventListener('pointermove', this.onPointerMove);
        window.removeEventListener('pointermove', this.onTouchableMove);
        if (event.pointerType === 'touch') {
            if (this.points.length === 1) {
                let target = event.target as HTMLElement;
                let savedEvent = this.points.find(e => e.pointerId == event.pointerId);
                if (savedEvent != null
                    && (event.timeStamp - savedEvent.timeStamp < 500)
                    && !this.isMovementStarted) {
                    if (this.isRequiredClass(target)) {
                        this.toggleFooterHeaderVisibility();
                    } else if (!this.footer.contains(target) && !this.header.contains(target)) {
                        this.blazorRef.invokeMethodAsync('Close');
                    }
                }
                this.isMovementStarted = false;
            }
            this.removeEvent(event);

            let imageWidth = this.round(this.media.getBoundingClientRect().width);
            let imageHeight = this.round(this.media.getBoundingClientRect().height);
            if (imageWidth < this.minWidth || imageHeight < this.minHeight) {
                this.media.style.width = this.minWidth + 'px';
                this.media.style.maxWidth = this.minWidth + 'px';
                this.media.style.maxHeight = this.minHeight + 'px';
                this.imageViewer.style.left = this.media.style.left = ((window.innerWidth - this.originMediaWidth) / 2) + 'px';
                this.imageViewer.style.top = this.media.style.left = ((window.innerHeight - this.originMediaHeight) / 2) + 'px';
            }
        } else if (event.pointerType === 'mouse') {
            let savedEvent = this.points.find(e => e.pointerId == event.pointerId);
            let target = event.target as HTMLElement;
            if (savedEvent != null
                && (event.timeStamp - savedEvent.timeStamp < 500)
                && this.isSameCoords(event, savedEvent)) {
                if (this.isRequiredClass(target)) {
                    this.toggleFooterHeaderVisibility();
                } else if (!this.footer.contains(target) && !this.header.contains(target)) {
                    this.blazorRef.invokeMethodAsync('Close');
                }
            }
            this.removeEvent(event);
        }
        this.curState.imageRect = this.curState.viewerRect = this.prevState.imageRect = this.prevState.viewerRect = this.media.getBoundingClientRect();
    };

    private onTouchableMove = (event: PointerEvent) => {
        if (!this.isMovementStarted)
            this.isMovementStarted = true;
        preventDefaultForEvent(event);
        const index = this.points.findIndex(
            (e) => e.pointerId === event.pointerId
        );
        this.points[index] = event;

        let windowWidth = window.innerWidth;
        let windowHeight = window.innerHeight;

        if (this.points.length === 2) {
            // two touches (zoom)
            this.curState.distance = this.getDistance();
            let scale = this.curState.distance / this.startDistance;
            let curImageRect = this.curState.imageRect;
            let isTooBig = curImageRect.width >= this.maxWidth || curImageRect.height >= this.maxHeight;
            if (isTooBig && this.curState.distance >= this.prevState.distance) {
                // don't enlarge image if it is too big
                this.startDistance = this.curState.distance;
                this.startImageRect = curImageRect;
            } else {
                this.scaleImage(scale);
            }
            this.prevState.distance = this.curState.distance;
            this.prevState.imageRect = this.prevState.viewerRect = this.curState.imageRect;
            this.curState.imageRect = this.curState.viewerRect = this.media.getBoundingClientRect();
        } else if (this.points.length === 1 && this.imageViewer.contains(event.target as HTMLElement)) {
            // one touch on image (move image)
            let viewer = this.imageViewer;
            if (this.canMoveImage(this.startImageRect)) {
                let rect = this.media.getBoundingClientRect();
                let imageTop = this.round(rect.top);
                let imageRight = this.round(rect.right);
                let imageBottom = this.round(rect.bottom);
                let imageLeft = this.round(rect.left);
                let deltaX = this.round(event.pageX - this.startX);
                let deltaY = this.round(event.pageY - this.startY);

                if (rect.width > windowWidth) {
                    if (event.pageX > this.prevX) {
                        // move right
                        if (imageLeft >= 0) {
                            viewer.style.left = 0 + 'px';
                            this.startX = event.pageX;
                        } else {
                            viewer.style.left = this.round(this.startImageRect.left + deltaX) + 'px';
                        }
                    } else if (event.pageX < this.prevX) {
                        // move left
                        if (imageRight <= windowWidth) {
                            viewer.style.left = this.round(windowWidth - this.startImageRect.width) + 'px';
                            this.startX = event.pageX;
                        } else {
                            viewer.style.left = this.round(this.startImageRect.left + deltaX) + 'px';
                        }
                    }
                    this.prevX = event.pageX;
                }

                if (rect.height > windowHeight) {
                    if (event.pageY > this.prevY) {
                        // move down
                        if (imageTop >= 0 && imageTop < 16) {
                            viewer.style.top = (this.startImageRect.top - this.startImageRect.top) + 'px';
                            this.startY = event.pageY;
                        } else {
                            viewer.style.top = this.round(this.startImageRect.top + deltaY) + 'px';
                        }
                    } else if (event.pageY < this.prevY) {
                        // move up
                        if (imageBottom <= windowHeight && imageBottom > windowHeight - 16) {
                            viewer.style.top = this.round(windowHeight - this.startImageRect.height) + 'px';
                            this.startY = event.pageY;
                        } else {
                            viewer.style.top = this.round(this.startImageRect.top + deltaY) + 'px';
                        }
                    }
                    this.prevY = event.pageY;
                }
                this.prevState.imageRect = rect;
            }
        }
    }

    private scaleImage(scale: number) {
        this.prevState.imageRect = this.prevState.viewerRect = this.curState.imageRect;
        let newImageWidth = this.round(this.startImageRect.width * scale);
        let newImageHeight = this.round(this.startImageRect.height * scale);
        this.media.style.width = this.media.style.maxWidth = newImageWidth + 'px';
        this.media.style.maxHeight = newImageHeight + 'px';
        this.curState.imageRect = this.curState.viewerRect = this.media.getBoundingClientRect();
        this.centerImage();
    }

    private canMoveImage(rect: DOMRect) : boolean {
        return rect.top < 0 || rect.right > window.innerWidth || rect.bottom > window.innerHeight || rect.left < 0;
    }

    private centerImage() {
        let curImageRect = this.curState.imageRect;
        let prevImageRect = this.prevState.imageRect;
        const imageLeft = this.round(curImageRect.left);
        const imageRight = this.round(curImageRect.right);
        const imageWidth = this.round(curImageRect.width);
        const windowWidth = window.innerWidth;
        const imageTop = this.round(curImageRect.top);
        const imageBottom = this.round(curImageRect.bottom);
        const imageHeight = this.round(curImageRect.height);
        const windowHeight = window.innerHeight;

        let left = imageLeft;
        let top = imageTop;
        let deltaX = (this.round(prevImageRect.width) - imageWidth) / 2;
        let deltaY = (this.round(prevImageRect.height) - imageHeight) / 2;
        let isScaleUp = curImageRect.width > prevImageRect.width;

        // center x
        if (curImageRect.width >= windowWidth && !isScaleUp) {
            left = prevImageRect.left + deltaX;
            if (curImageRect.left >= 0) {
                left = 0;
            } else if (curImageRect.right <= windowWidth) {
                left = windowWidth - curImageRect.width;
            }
        } else {
            if ((windowWidth / 2 - imageLeft) != (imageRight - windowWidth / 2)) {
                left = (this.prevState.imageRect.left + deltaX);
            }
        }

        // center y
        if (curImageRect.height >= windowHeight && !isScaleUp) {
            top = (this.prevState.imageRect.top + deltaY);
            if (curImageRect.top > 0) {
                top = 0;
            } else if (curImageRect.bottom <= windowHeight) {
                top = windowHeight - curImageRect.height;
            }
        } else {
            if ((windowHeight / 2 - imageTop) != (imageBottom - windowHeight / 2)) {
                top = (this.prevState.imageRect.top + deltaY);
            }
        }

        this.prevState.imageRect = curImageRect;
        this.imageViewer.style.left = left + 'px';
        this.imageViewer.style.top = top + 'px';
        this.curState.imageRect = this.curState.viewerRect = this.media.getBoundingClientRect();
    }

    private isSameCoords(event1: PointerEvent, event2: PointerEvent) : boolean {
        let result = false;
        let deltaX = Math.abs(event1.x - event2.x);
        let deltaY = Math.abs(event1.y - event2.y);
        if (deltaX < 10 && deltaY < 10)
            result = true;
        return result;
    }
}

