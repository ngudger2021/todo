(() => {
    const taskGrid = document.getElementById('taskGrid');
    const taskTemplate = document.getElementById('taskCardTemplate');
    const subtaskTemplate = document.getElementById('subtaskTemplate');
    const taskModalEl = document.getElementById('taskModal');
    const taskModal = new bootstrap.Modal(taskModalEl);
    const form = document.getElementById('taskForm');
    const errorBox = document.getElementById('formError');
    const dataPathLabel = document.getElementById('dataPathLabel');

    const statusFilter = document.getElementById('statusFilter');
    const priorityFilter = document.getElementById('priorityFilter');
    const searchInput = document.getElementById('searchInput');

    let tasks = [];

    const api = {
        async fetchTasks() {
            const res = await fetch('/api/tasks');
            if (!res.ok) throw new Error('Unable to load tasks');
            return res.json();
        },
        async fetchHistory() {
            const res = await fetch('/api/history');
            if (!res.ok) throw new Error('Unable to load history');
            return res.json();
        },
        async createTask(payload) {
            const res = await fetch('/api/tasks', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            if (!res.ok) throw new Error('Create failed');
            return res.json();
        },
        async updateTask(id, payload) {
            const res = await fetch(`/api/tasks/${id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            if (!res.ok) throw new Error('Update failed');
            return res.json();
        },
        async deleteTask(id) {
            const res = await fetch(`/api/tasks/${id}`, { method: 'DELETE' });
            if (!res.ok) throw new Error('Delete failed');
        }
    };

    function formatDate(dateStr) {
        if (!dateStr) return null;
        const date = new Date(dateStr);
        if (Number.isNaN(date.getTime())) return null;
        return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
    }

    function applyFilters(list) {
        const status = statusFilter.value;
        const priority = priorityFilter.value;
        const search = searchInput.value.trim().toLowerCase();

        return list.filter(task => {
            if (status === 'pending' && task.completed) return false;
            if (status === 'completed' && !task.completed) return false;
            if (priority !== 'all' && task.priority !== priority) return false;

            if (search) {
                const haystack = `${task.title} ${task.description} ${(task.tags || []).join(' ')}`.toLowerCase();
                if (!haystack.includes(search)) return false;
            }
            return true;
        });
    }

    function renderEmptyState() {
        taskGrid.innerHTML = '<div class="col-12"><div class="empty-state glassy rounded-4">No tasks match the filters. Create something inspiring!</div></div>';
    }

    function renderSubtasks(container, task) {
        container.innerHTML = '';
        if (!task.subtasks || task.subtasks.length === 0) {
            return;
        }

        task.subtasks.forEach(sub => {
            const row = document.createElement('div');
            row.className = 'd-flex align-items-start justify-content-between subtasks-row border rounded-3 p-2 mb-1';
            row.innerHTML = `<div><div class="fw-semibold">${sub.title || 'Untitled subtask'}</div><div class="text-muted small">${sub.description || ''}</div></div><span class="badge ${sub.completed ? 'badge-success-soft' : 'badge-info-soft'}">${sub.completed ? 'Done' : 'Open'}</span>`;
            container.appendChild(row);
        });
    }

    function renderTags(container, tags = []) {
        container.innerHTML = '';
        if (!tags || tags.length === 0) {
            const span = document.createElement('span');
            span.className = 'badge rounded-pill badge-info-soft';
            span.innerText = 'No tags';
            container.appendChild(span);
            return;
        }
        tags.forEach(tag => {
            const badge = document.createElement('span');
            badge.className = 'badge rounded-pill badge-info-soft';
            badge.textContent = tag;
            container.appendChild(badge);
        });
    }

    function renderTasks() {
        const filtered = applyFilters(tasks);
        taskGrid.innerHTML = '';
        if (filtered.length === 0) {
            renderEmptyState();
            return;
        }

        filtered.forEach(task => {
            const clone = taskTemplate.content.cloneNode(true);
            const card = clone.querySelector('.task-card');
            clone.querySelector('.card-title').textContent = task.title || 'Untitled task';
            clone.querySelector('.description').textContent = task.description || 'No description yet';

            const priorityBadge = clone.querySelector('.priority-badge');
            priorityBadge.textContent = task.priority;
            priorityBadge.classList.add(task.priority === 'High' ? 'badge-danger-soft' : task.priority === 'Low' ? 'badge-info-soft' : 'badge-warning-soft');

            const statusBadge = clone.querySelector('.status-badge');
            statusBadge.textContent = task.completed ? 'Completed' : 'Pending';
            statusBadge.classList.add(task.completed ? 'badge-success-soft' : 'badge-info-soft');

            const dueDate = clone.querySelector('.due-date');
            const formatted = formatDate(task.due_date || task.dueDate);
            dueDate.innerHTML = formatted ? `<i class="bi bi-calendar-event me-1"></i>${formatted}` : '<i class="bi bi-calendar-event me-1"></i>No due date';

            renderTags(clone.querySelector('.tags'), task.tags);
            renderSubtasks(clone.querySelector('.subtasks'), task);

            clone.querySelector('.toggle-complete').addEventListener('click', () => toggleComplete(task));
            clone.querySelector('.edit-task').addEventListener('click', () => openEditModal(task));
            clone.querySelector('.delete-task').addEventListener('click', () => confirmDelete(task));

            taskGrid.appendChild(clone);
        });
    }

    function subtaskFormValues() {
        const items = [];
        document.querySelectorAll('#subtasksContainer .card').forEach(card => {
            const title = card.querySelector('.subtask-title').value.trim();
            const description = card.querySelector('.subtask-description').value.trim();
            const priority = card.querySelector('.subtask-priority').value;
            const due = card.querySelector('.subtask-due').value;
            items.push({
                title,
                description,
                priority,
                completed: false,
                due_date: due || null,
                attachments: [],
                tags: []
            });
        });
        return items;
    }

    function fillSubtasksForm(subtasks = []) {
        const container = document.getElementById('subtasksContainer');
        container.innerHTML = '';
        subtasks.forEach(sub => addSubtaskRow(sub));
    }

    function addSubtaskRow(subtask = {}) {
        const node = subtaskTemplate.content.cloneNode(true);
        const card = node.querySelector('.card');
        card.querySelector('.subtask-title').value = subtask.title || '';
        card.querySelector('.subtask-description').value = subtask.description || '';
        card.querySelector('.subtask-priority').value = subtask.priority || 'Medium';
        card.querySelector('.subtask-due').value = (subtask.due_date || '').split('T')[0] || '';
        card.querySelector('.remove-subtask').addEventListener('click', () => card.remove());
        document.getElementById('subtasksContainer').appendChild(node);
    }

    function openEditModal(task) {
        document.getElementById('taskModalLabel').textContent = 'Edit Task';
        form.reset();
        errorBox.textContent = '';
        document.getElementById('taskId').value = task.id;
        document.getElementById('taskTitle').value = task.title || '';
        document.getElementById('taskDescription').value = task.description || '';
        document.getElementById('taskPriority').value = task.priority || 'Medium';
        document.getElementById('taskDueDate').value = (task.due_date || '').split('T')[0] || '';
        document.getElementById('taskTags').value = (task.tags || []).join(', ');
        document.getElementById('taskCompleted').checked = !!task.completed;
        fillSubtasksForm(task.subtasks || []);
        taskModal.show();
    }

    function openNewModal() {
        document.getElementById('taskModalLabel').textContent = 'New Task';
        form.reset();
        errorBox.textContent = '';
        document.getElementById('taskId').value = '';
        document.getElementById('subtasksContainer').innerHTML = '';
        taskModal.show();
    }

    function buildPayload() {
        const title = document.getElementById('taskTitle').value.trim();
        if (!title) {
            throw new Error('Title is required');
        }

        const due = document.getElementById('taskDueDate').value;
        const tags = document.getElementById('taskTags').value
            .split(',')
            .map(t => t.trim())
            .filter(Boolean);

        return {
            title,
            description: document.getElementById('taskDescription').value,
            priority: document.getElementById('taskPriority').value || 'Medium',
            due_date: due || null,
            completed: document.getElementById('taskCompleted').checked,
            subtasks: subtaskFormValues(),
            tags,
            attachments: []
        };
    }

    async function saveTask(evt) {
        evt.preventDefault();
        errorBox.textContent = '';
        try {
            const payload = buildPayload();
            const id = document.getElementById('taskId').value;
            if (id) {
                await api.updateTask(id, { ...payload, id });
            } else {
                await api.createTask(payload);
            }
            taskModal.hide();
            await loadTasks();
        } catch (err) {
            errorBox.textContent = err.message || 'Unable to save task';
        }
    }

    async function toggleComplete(task) {
        const payload = { ...task, completed: !task.completed };
        try {
            await api.updateTask(task.id, payload);
            await loadTasks();
        } catch (err) {
            alert(err.message || 'Unable to update task');
        }
    }

    async function confirmDelete(task) {
        if (!confirm(`Delete "${task.title}"? This cannot be undone.`)) return;
        try {
            await api.deleteTask(task.id);
            await loadTasks();
        } catch (err) {
            alert(err.message || 'Unable to delete task');
        }
    }

    async function loadTasks() {
        try {
            tasks = await api.fetchTasks();
            renderTasks();
        } catch (err) {
            taskGrid.innerHTML = `<div class="col-12"><div class="alert alert-danger">${err.message}</div></div>`;
        }
    }

    async function loadDataPath() {
        try {
            const history = await api.fetchHistory();
            const hasHistory = Array.isArray(history) && history.length > 0;
            dataPathLabel.textContent = hasHistory ? 'todo_data.json (with history)' : 'todo_data.json';
        } catch {
            dataPathLabel.textContent = 'todo_data.json';
        }
    }

    function registerEvents() {
        document.getElementById('newTaskBtn').addEventListener('click', openNewModal);
        document.getElementById('addSubtaskBtn').addEventListener('click', () => addSubtaskRow());
        document.getElementById('refreshBtn').addEventListener('click', loadTasks);
        statusFilter.addEventListener('change', renderTasks);
        priorityFilter.addEventListener('change', renderTasks);
        searchInput.addEventListener('input', () => {
            clearTimeout(searchInput._debounce);
            searchInput._debounce = setTimeout(renderTasks, 120);
        });
        form.addEventListener('submit', saveTask);
    }

    registerEvents();
    loadTasks();
    loadDataPath();
})();
